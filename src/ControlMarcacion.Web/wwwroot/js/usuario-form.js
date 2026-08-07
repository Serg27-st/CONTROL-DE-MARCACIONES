// Formulario de alta de usuario:
//   rol     → muestra u oculta el bloque de plantas (solo ADMINISTRADOR lo usa)
//   empresa → repuebla la lista de plantas
//
// Es comodidad de pantalla, nada más: el servidor descarta las plantas cuando el rol no
// es ADMINISTRADOR y valida que cada id exista, aunque el POST llegue armado a mano.
//
// Los manejadores se enganchan desde aquí y no con atributos onclick en el HTML:
// un nombre con apóstrofo dentro de un atributo rompe el marcado y abre XSS.

(function () {
    "use strict";

    var ROL_CON_PLANTAS = "ADMINISTRADOR";

    var rol = document.getElementById("RolUsuario");
    var empresa = document.getElementById("EmpresaId");
    var bloque = document.getElementById("bloque-plantas");
    var lista = document.getElementById("lista-plantas");

    if (!rol || !empresa || !bloque || !lista) {
        return;
    }

    function seleccionadas() {
        var marcadas = lista.querySelectorAll("input[type=checkbox]:checked");
        var valores = [];

        Array.prototype.forEach.call(marcadas, function (casilla) {
            valores.push(casilla.value);
        });

        return valores;
    }

    function repoblar(opciones, previas) {
        lista.innerHTML = "";

        if (opciones.length === 0) {
            var vacio = document.createElement("p");
            vacio.style.color = "var(--color-texto-suave)";
            vacio.textContent = "No hay plantas para esta empresa.";
            lista.appendChild(vacio);
            return;
        }

        opciones.forEach(function (opcion) {
            var etiqueta = document.createElement("label");
            etiqueta.style.display = "block";
            etiqueta.style.fontWeight = "normal";

            var casilla = document.createElement("input");
            casilla.type = "checkbox";
            casilla.name = "Datos.PlantaIds";
            casilla.value = opcion.id;
            casilla.checked = previas.indexOf(String(opcion.id)) >= 0;

            etiqueta.appendChild(casilla);
            // textContent, nunca innerHTML: el nombre viene de la base de datos.
            etiqueta.appendChild(document.createTextNode(" " + opcion.nombre));

            lista.appendChild(etiqueta);
        });
    }

    function alternarBloque() {
        var aplica = rol.value === ROL_CON_PLANTAS;

        bloque.hidden = !aplica;

        if (!aplica) {
            // Se limpian las marcas para que un cambio de rol no arrastre plantas que
            // el usuario ya no ve en pantalla.
            Array.prototype.forEach.call(
                lista.querySelectorAll("input[type=checkbox]"),
                function (casilla) { casilla.checked = false; });
        }
    }

    rol.addEventListener("change", alternarBloque);

    empresa.addEventListener("change", function () {
        var previas = seleccionadas();
        var url = "/Usuarios/PlantasDeEmpresa?empresaId=" + encodeURIComponent(empresa.value || "0");

        fetch(url, { headers: { "Accept": "application/json" } })
            .then(function (respuesta) {
                if (!respuesta.ok) {
                    throw new Error("No se pudo cargar el catálogo.");
                }
                return respuesta.json();
            })
            .then(function (datos) {
                repoblar(datos, previas);
            })
            .catch(function () {
                repoblar([], previas);
            });
    });

    alternarBloque();
})();
