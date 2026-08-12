// Acota el campo "número de documento" a lo que admite el tipo elegido: 8 dígitos exactos
// para el DNI, hasta 10 caracteres para el resto.
//
// Las reglas NO están escritas aquí. Viajan desde el catálogo (TipoDocumento) hasta los
// atributos data-* de cada <option>, así que ampliar el carné de extranjería a 12 sigue
// siendo un UPDATE en la tabla y no un cambio en este archivo.
//
// Esto es comodidad para quien escribe, no un control de acceso: un maxlength se quita
// desde las herramientas del navegador, y la carga masiva por CSV no pasa por aquí en
// absoluto. La validación que decide vive en Application (ValidadorDocumentoIdentidad).
(function () {
    "use strict";

    // El <select> declara a qué input aplica con data-restringe-documento="#Id_Del_Input".
    var SELECTOR = "select[data-restringe-documento]";

    function opcionElegida(select) {
        return select.options[select.selectedIndex] || null;
    }

    function entero(valor) {
        var n = parseInt(valor, 10);
        return isNaN(n) ? null : n;
    }

    /** Texto de ayuda bajo el campo. Se crea una sola vez y luego solo cambia su contenido. */
    function nota(input) {
        var existente = input.parentNode.querySelector(".nota-documento");

        if (existente) {
            return existente;
        }

        var creada = document.createElement("p");
        creada.className = "nota-limite nota-documento";

        // Después del input pero antes del <span> de error de validación, para que el
        // mensaje del servidor quede siempre como la última línea del campo.
        input.parentNode.insertBefore(creada, input.nextSibling);
        return creada;
    }

    function soloDigitos(input) {
        return (input.value || "").replace(/\D+/g, "");
    }

    function aplicar(select, input, recortar) {
        var opcion = opcionElegida(select);

        if (!opcion || !opcion.value) {
            input.removeAttribute("maxlength");
            input.removeAttribute("inputmode");
            nota(input).textContent = "";
            return;
        }

        var maximo = entero(opcion.getAttribute("data-maximo"));
        var exacta = entero(opcion.getAttribute("data-exacta"));
        var digitos = opcion.getAttribute("data-solo-digitos") === "1";
        var unidad = digitos ? "dígitos" : "caracteres";

        if (maximo) {
            input.setAttribute("maxlength", String(maximo));
        }

        if (digitos) {
            // inputmode y no type="number": un documento es texto. Con type="number" el
            // navegador borra los ceros a la izquierda y el cruce con la planilla falla.
            input.setAttribute("inputmode", "numeric");
        } else {
            input.removeAttribute("inputmode");
        }

        nota(input).textContent = exacta
            ? "Debe tener exactamente " + exacta + " " + unidad + "."
            : maximo
                ? "Admite hasta " + maximo + " " + unidad + "."
                : "";

        // Al cambiar de tipo, lo ya escrito puede no caber en la regla nueva. Se recorta
        // en vez de dejar un valor que el servidor va a rechazar sin que se vea por qué.
        if (!recortar) {
            return;
        }

        if (digitos) {
            input.value = soloDigitos(input);
        }

        if (maximo && input.value.length > maximo) {
            input.value = input.value.slice(0, maximo);
        }
    }

    function conectar(select) {
        // El panel flotante de Personal llega por fetch + innerHTML y puede reinyectarse
        // varias veces. Sin esta marca, cada apertura engancharía otro par de manejadores
        // sobre el mismo campo.
        if (select.dataset.documentoConectado === "1") {
            return;
        }

        var input = document.querySelector(select.getAttribute("data-restringe-documento"));

        if (!input) {
            return;
        }

        select.dataset.documentoConectado = "1";

        // Al cargar no se recorta: si la ficha guardada trae un valor que ya no cumple la
        // regla vigente, borrarlo en silencio al abrir la pantalla escondería el problema.
        // Se muestra tal cual y el servidor lo rechaza al guardar, con su motivo.
        aplicar(select, input, false);

        select.addEventListener("change", function () {
            aplicar(select, input, true);
        });

        input.addEventListener("input", function () {
            var opcion = opcionElegida(select);

            if (opcion && opcion.getAttribute("data-solo-digitos") === "1") {
                var limpio = soloDigitos(input);

                if (limpio !== input.value) {
                    input.value = limpio;
                }
            }
        });
    }

    function iniciar() {
        var selects = document.querySelectorAll(SELECTOR);

        for (var i = 0; i < selects.length; i++) {
            conectar(selects[i]);
        }
    }

    // Mismo mecanismo que searchable-select.js: el panel flotante de Personal se inyecta
    // con innerHTML mucho después de DOMContentLoaded, así que se vigila el árbol en vez
    // de pedirle a panel-trabajador.js que llame a nada de este archivo.
    function observar() {
        if (typeof MutationObserver !== "function") {
            return;
        }

        new MutationObserver(function (mutaciones) {
            for (var i = 0; i < mutaciones.length; i++) {
                if (mutaciones[i].addedNodes.length > 0) {
                    iniciar();
                    return;
                }
            }
        }).observe(document.body, { childList: true, subtree: true });
    }

    function arrancar() {
        iniciar();
        observar();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", arrancar);
    } else {
        arrancar();
    }
})();
