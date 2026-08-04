// Encadena los desplegables del formulario de personal:
//   empresa → planta → área → sección
//   planta  → turnos habilitados
//
// El turno depende de la planta porque el turno especial de sábado solo existe en
// Planta 01, y esa restricción vive en el catálogo, no en el código.
//
// Los manejadores se enganchan desde aquí y no con atributos onclick en el HTML:
// un nombre con apóstrofo dentro de un atributo rompe el marcado y abre XSS.

(function () {
    "use strict";

    var empresa = document.getElementById("EmpresaId");
    var planta = document.getElementById("PlantaId");
    var area = document.getElementById("AreaId");
    var seccion = document.getElementById("SeccionId");
    var turno = document.getElementById("TurnoId");

    if (!empresa || !planta || !area || !seccion || !turno) {
        return;
    }

    function repoblar(select, opciones, textoVacio) {
        select.innerHTML = "";

        var vacia = document.createElement("option");
        vacia.value = "";
        vacia.textContent = textoVacio;
        select.appendChild(vacia);

        opciones.forEach(function (opcion) {
            var elemento = document.createElement("option");
            elemento.value = opcion.id;
            // textContent, nunca innerHTML: el nombre viene de la base de datos.
            elemento.textContent = opcion.nombre;
            select.appendChild(elemento);
        });
    }

    function cargar(url, destino, textoVacio) {
        return fetch(url, { headers: { "Accept": "application/json" } })
            .then(function (respuesta) {
                if (!respuesta.ok) {
                    throw new Error("No se pudo cargar el catálogo.");
                }
                return respuesta.json();
            })
            .then(function (datos) {
                repoblar(destino, datos, textoVacio);
            })
            .catch(function () {
                repoblar(destino, [], "No se pudo cargar");
            });
    }

    empresa.addEventListener("change", function () {
        repoblar(area, [], "Seleccione una planta…");
        repoblar(seccion, [], "Seleccione un área…");

        if (!empresa.value) {
            repoblar(planta, [], "Seleccione una empresa…");
            repoblar(turno, [], "Sin turno asignado");
            return;
        }

        cargar("/Trabajadores/PlantasDeEmpresa?empresaId=" + encodeURIComponent(empresa.value),
               planta,
               "Seleccione…");
    });

    planta.addEventListener("change", function () {
        repoblar(seccion, [], "Seleccione un área…");

        if (!planta.value) {
            repoblar(area, [], "Seleccione una planta…");
            return;
        }

        cargar("/Trabajadores/AreasDePlanta?plantaId=" + encodeURIComponent(planta.value),
               area,
               "Seleccione…");

        cargar("/Trabajadores/TurnosDePlanta?plantaId=" + encodeURIComponent(planta.value),
               turno,
               "Sin turno asignado");
    });

    area.addEventListener("change", function () {
        if (!area.value) {
            repoblar(seccion, [], "Seleccione un área…");
            return;
        }

        cargar("/Trabajadores/SeccionesDeArea?areaId=" + encodeURIComponent(area.value),
               seccion,
               "Seleccione…");
    });
})();
