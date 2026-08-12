// Formulario de alta de una solicitud de ausencia.
//
// Muestra u oculta los campos propios de vacaciones y avisa del tope de días del tipo
// elegido. Dos aclaraciones sobre lo que este archivo NO hace:
//
//   1. No decide los topes. Cada <option> del tipo de ausencia trae los suyos en
//      data-maximo-evento / data-maximo-anio, que salen del catálogo. Si GCA cambia el
//      tope de descanso médico a 25 días, el aviso cambia solo: aquí no hay ningún 20.
//   2. No valida de verdad. Todo lo que comprueba se vuelve a comprobar en el servidor
//      (ValidadorLimitesSolicitud). Esto solo evita el viaje de ida y vuelta.

(function () {
    "use strict";

    var GOZADAS = "GOZADAS";
    var VENTA = "VENTA";
    var FORMATO_PERIODO = /^\d{4}-\d{4}$/;

    var tipo = document.getElementById("tipo-ausencia");
    var subtipo = document.getElementById("subtipo-vacaciones");
    var campoSubtipo = document.getElementById("campo-subtipo-vacaciones");
    var campoPeriodoGozadas = document.getElementById("campo-periodo-gozadas");
    var campoPeriodoVenta = document.getElementById("campo-periodo-venta");
    var periodoGozadas = document.getElementById("periodo-gozadas");
    var periodoVenta = document.getElementById("periodo-venta");
    var avisoLimite = document.getElementById("aviso-limite");
    var avisoSubtipo = document.getElementById("aviso-subtipo");
    var errorPeriodoVenta = document.getElementById("error-periodo-venta");

    var bloqueClinico = document.getElementById("bloque-detalle-clinico");
    var camposClinicos = [
        document.getElementById("contingencia"),
        document.getElementById("centro-atencion"),
        document.getElementById("diagnostico")
    ].filter(function (c) { return c !== null; });

    if (!tipo || !subtipo || !campoSubtipo || !periodoGozadas || !periodoVenta) {
        return;
    }

    var formulario = tipo.form;

    function mostrar(elemento, visible) {
        if (elemento) {
            elemento.hidden = !visible;
        }
    }

    // Un control deshabilitado no se envía. Es lo que garantiza que, habiendo dos
    // controles con name="Datos.PeriodoVacacional", al servidor llegue uno solo.
    function habilitar(control, activo) {
        control.disabled = !activo;

        if (!activo) {
            control.value = "";
        }
    }

    function texto(elemento, mensaje) {
        if (!elemento) {
            return;
        }

        // textContent, nunca innerHTML: el nombre del tipo viene de la base de datos.
        elemento.textContent = mensaje || "";
        elemento.hidden = !mensaje;
    }

    function opcionElegida() {
        return tipo.selectedIndex >= 0 ? tipo.options[tipo.selectedIndex] : null;
    }

    function entero(valor) {
        var numero = parseInt(valor, 10);

        return isNaN(numero) ? null : numero;
    }

    // "98 días (14 semanas)" cuando el tope es múltiplo de siete, que es como enuncia la
    // norma la licencia por maternidad; "10 días" en cualquier otro caso.
    function enDias(cantidad) {
        return cantidad % 7 === 0
            ? cantidad + " días (" + (cantidad / 7) + " semanas)"
            : cantidad + " días";
    }

    function avisoDelTipo(opcion) {
        if (!opcion) {
            return "";
        }

        var porEvento = entero(opcion.getAttribute("data-maximo-evento"));

        if (porEvento !== null) {
            return "Máximo " + enDias(porEvento) + ".";
        }

        var porAnio = entero(opcion.getAttribute("data-maximo-anio"));

        if (porAnio !== null) {
            return "Máximo " + enDias(porAnio) + " completos o acumulados por año.";
        }

        return "";
    }

    function avisoDelSubtipo(valor) {
        if (valor === VENTA) {
            return "Máximo 15 días por periodo.";
        }

        if (valor === GOZADAS) {
            return "Máximo 30 días por periodo.";
        }

        return "";
    }

    function periodoValido(valor) {
        if (!FORMATO_PERIODO.test(valor)) {
            return false;
        }

        // La expresión regular acepta 2025-2027: el segundo año tiene que ser el siguiente.
        return parseInt(valor.substring(5, 9), 10) === parseInt(valor.substring(0, 4), 10) + 1;
    }

    function refrescarSubtipo() {
        var valor = subtipo.value;
        var esVenta = valor === VENTA;
        var esGozadas = valor === GOZADAS;

        mostrar(campoPeriodoVenta, esVenta);
        mostrar(campoPeriodoGozadas, esGozadas);

        habilitar(periodoVenta, esVenta);
        habilitar(periodoGozadas, esGozadas);

        texto(avisoSubtipo, avisoDelSubtipo(valor));
        texto(errorPeriodoVenta, "");
    }

    // El detalle clínico solo aplica a los tipos subsidiados. Qué tipos son lo dice el
    // catálogo en data-detalle-clinico: aquí no hay ninguna lista de códigos que se quede
    // desfasada cuando GCA dé de alta otro tipo subsidiado.
    function refrescarDetalleClinico(opcion) {
        var exige = opcion !== null && opcion.getAttribute("data-detalle-clinico") === "1";

        mostrar(bloqueClinico, exige);

        camposClinicos.forEach(function (campo) {
            // required se pone y se quita junto con la visibilidad. Un campo required y
            // oculto hace que el navegador cancele el envío con un aviso que no puede
            // mostrar, porque no logra enfocar el control: el formulario queda mudo.
            campo.required = exige;

            // Al dejar de aplicar se vacía. El servidor igual lo descarta, pero no tiene
            // sentido mandar un diagnóstico —dato sensible de salud— en una solicitud de
            // permiso o de vacaciones.
            if (!exige) {
                campo.value = "";
            }
        });
    }

    function refrescarTipo() {
        var opcion = opcionElegida();
        var esVacaciones = opcion !== null && opcion.getAttribute("data-codigo") === "VAC";

        mostrar(campoSubtipo, esVacaciones);
        texto(avisoLimite, avisoDelTipo(opcion));

        if (!esVacaciones) {
            subtipo.value = "";
        }

        refrescarDetalleClinico(opcion);
        refrescarSubtipo();
    }

    tipo.addEventListener("change", refrescarTipo);
    subtipo.addEventListener("change", refrescarSubtipo);

    if (formulario) {
        formulario.addEventListener("submit", function (evento) {
            if (periodoVenta.disabled) {
                return;
            }

            if (periodoValido(periodoVenta.value.trim())) {
                texto(errorPeriodoVenta, "");
                return;
            }

            evento.preventDefault();
            texto(
                errorPeriodoVenta,
                "Escriba el periodo como AAAA-AAAA, con años consecutivos (por ejemplo, 2025-2026).");
            periodoVenta.focus();
        });
    }

    // Al volver del servidor con errores de validación, el tipo y el subtipo ya vienen
    // elegidos: hay que dejar la pantalla como estaba, no colapsada.
    refrescarTipo();
})();
