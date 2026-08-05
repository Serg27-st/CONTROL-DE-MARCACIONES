// Encadena los filtros de organización de las pantallas de reportes:
//   empresa → planta → área → sección
//
// Reutiliza los mismos endpoints de catálogo que el formulario de personal
// (no hay nada específico de reportes en ellos), pero sin el turno: aquí no aplica.

(function () {
    "use strict";

    var empresa = document.getElementById("EmpresaId");
    var planta = document.getElementById("PlantaId");
    var area = document.getElementById("AreaId");
    var seccion = document.getElementById("SeccionId");

    if (!empresa || !planta || !area || !seccion) {
        return;
    }

    function repoblar(select, opciones, textoVacio) {
        var seleccionActual = select.value;
        select.innerHTML = "";

        var vacia = document.createElement("option");
        vacia.value = "";
        vacia.textContent = textoVacio;
        select.appendChild(vacia);

        opciones.forEach(function (opcion) {
            var elemento = document.createElement("option");
            elemento.value = opcion.id;
            elemento.textContent = opcion.nombre;
            if (String(opcion.id) === seleccionActual) {
                elemento.selected = true;
            }
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
        if (!empresa.value) {
            repoblar(planta, [], "Todas");
            repoblar(area, [], "Seleccione una planta…");
            repoblar(seccion, [], "Seleccione un área…");
            return;
        }

        cargar("/Trabajadores/PlantasDeEmpresa?empresaId=" + encodeURIComponent(empresa.value), planta, "Todas");
    });

    planta.addEventListener("change", function () {
        if (!planta.value) {
            repoblar(area, [], "Todas");
            repoblar(seccion, [], "Seleccione un área…");
            return;
        }

        cargar("/Trabajadores/AreasDePlanta?plantaId=" + encodeURIComponent(planta.value), area, "Todas");
    });

    area.addEventListener("change", function () {
        if (!area.value) {
            repoblar(seccion, [], "Todas");
            return;
        }

        cargar("/Trabajadores/SeccionesDeArea?areaId=" + encodeURIComponent(area.value), seccion, "Todas");
    });
})();
