// Filtro en vivo sobre las filas que ya están en la página.
//
// No consulta al servidor: se usa en tablas que caben enteras en la respuesta —los
// catálogos son decenas o cientos de filas— donde un viaje por cada tecla sería más lento
// sin traer ninguna fila nueva. Para listados que paginan (personal, tareo) el filtro
// correcto es el del formulario, porque ahí sí hay datos que no llegaron.
//
// Se activa por atributos, sin nombres codificados aquí:
//   data-filtro-tabla="id-de-la-tabla"    la tabla cuyas filas del <tbody> se filtran
//   data-filtro-conteo="id-del-contador"  (opcional) elemento donde escribir cuántas quedan
//   data-filtro-sustantivo="feriado(s)"   (opcional) qué se cuenta; por omisión "registro(s)"

(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var entradas = document.querySelectorAll("[data-filtro-tabla]");

        Array.prototype.forEach.call(entradas, function (entrada) {
            var tabla = document.getElementById(entrada.getAttribute("data-filtro-tabla"));

            if (!tabla) {
                return;
            }

            var cuerpo = tabla.tBodies[0];

            if (!cuerpo) {
                return;
            }

            var contador = document.getElementById(entrada.getAttribute("data-filtro-conteo"));
            var plantillaConteo = contador ? contador.textContent.trim() : "";
            var filas = Array.prototype.slice.call(cuerpo.rows);

            // Lo que se está contando. Sin esto el pie de una tabla de feriados pasaba de
            // "13 feriado(s)" a "2 de 13 registro(s)" en cuanto se escribía una letra.
            var sustantivo = entrada.getAttribute("data-filtro-sustantivo") || "registro(s)";

            // Se normaliza una vez y no en cada pulsación: quitar tildes con
            // Unicode fila por fila y tecla por tecla se nota en una tabla larga.
            var textos = filas.map(function (fila) {
                return normalizar(fila.textContent);
            });

            // Se compara sin tildes: en la planta se escribe "seccion" y "produccion" sin
            // acentuar, y una coincidencia exacta no encontraría nada. El rango
            // U+0300–U+036F son las marcas diacríticas que NFD deja sueltas; va escrito
            // con escapes y no con los caracteres literales, que son invisibles en el
            // editor y desaparecen al primer copiar y pegar.
            function normalizar(texto) {
                return texto
                    .normalize("NFD")
                    .replace(/[\u0300-\u036f]/g, "")
                    .toLowerCase();
            }

            function filtrar() {
                var buscado = normalizar(entrada.value.trim());
                var visibles = 0;

                for (var i = 0; i < filas.length; i++) {
                    var coincide = buscado === "" || textos[i].indexOf(buscado) >= 0;

                    filas[i].hidden = !coincide;

                    if (coincide) {
                        visibles++;
                    }
                }

                if (!contador) {
                    return;
                }

                contador.textContent = buscado === ""
                    ? plantillaConteo
                    : visibles + " de " + filas.length + " " + sustantivo;
            }

            entrada.addEventListener("input", filtrar);

            // Escape limpia el filtro: es lo que espera quien acaba de escribir y no
            // encontró nada, y evita dejar la tabla "vacía" sin razón aparente.
            entrada.addEventListener("keydown", function (evento) {
                if (evento.key === "Escape" && entrada.value !== "") {
                    entrada.value = "";
                    filtrar();
                }
            });

            // Al volver atrás el navegador restaura el texto escrito pero no dispara
            // "input": sin esto la tabla saldría completa con el filtro puesto.
            filtrar();
        });
    });
})();
