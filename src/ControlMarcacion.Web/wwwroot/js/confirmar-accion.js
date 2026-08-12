// Pide confirmación antes de una acción con consecuencias.
//
// El texto viaja en un atributo data-*, no dentro de un onclick: un nombre de catálogo
// con apóstrofo ("Sección de Producción 'A'") rompería el atributo y abriría XSS.

(function () {
    "use strict";

    document.addEventListener("click", function (evento) {
        var boton = evento.target.closest("[data-confirmar]");

        if (!boton) {
            return;
        }

        if (!window.confirm(boton.getAttribute("data-confirmar"))) {
            evento.preventDefault();
        }
    });
})();
