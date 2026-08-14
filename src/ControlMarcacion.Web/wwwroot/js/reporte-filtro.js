// Filtros de las pantallas de reportes. Dos cosas independientes:
//
//   1. El alcance temporal (periodo / mes / día / rango): muestra los campos del alcance
//      elegido y DESHABILITA los del resto.
//   2. La cascada de organización: empresa → planta → centro → centro de costo.
//
// Deshabilitar y no solo ocultar es lo que hace que esto funcione: los bloques de "Día" y
// "Rango" tienen los dos un input llamado "Desde", y si ambos viajan en la query string el
// binding recibe dos valores para el mismo campo. Un input deshabilitado no se envía, así
// que el servidor recibe exactamente los campos del alcance elegido y nada más.
//
// Sin JS el formulario sigue siendo usable: se ven los cuatro bloques a la vez y el
// servidor lee los que correspondan al radio marcado.

(function () {
    "use strict";

    var formulario = document.getElementById("filtro-reporte");

    if (!formulario) {
        return;
    }

    var bloques = formulario.querySelectorAll("[data-alcance]");
    var radios = formulario.querySelectorAll("input[name='Alcance']");

    function alcanceElegido() {
        for (var i = 0; i < radios.length; i++) {
            if (radios[i].checked) {
                return radios[i].value;
            }
        }

        return "Periodo";
    }

    function aplicarAlcance() {
        var elegido = alcanceElegido();

        bloques.forEach(function (bloque) {
            var aplica = bloque.getAttribute("data-alcance") === elegido;

            bloque.hidden = !aplica;

            bloque.querySelectorAll("input, select").forEach(function (campo) {
                campo.disabled = !aplica;
            });
        });
    }

    radios.forEach(function (radio) {
        radio.addEventListener("change", aplicarAlcance);
    });

    aplicarAlcance();
})();

(function () {
    "use strict";

    var empresa = document.getElementById("EmpresaId");
    var planta = document.getElementById("PlantaId");
    var centro = document.getElementById("CentroId");
    var centroCosto = document.getElementById("CentroCostoId");

    if (!empresa || !planta || !centro || !centroCosto) {
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
            repoblar(centro, [], "Todos");
            repoblar(centroCosto, [], "Todos");
            return;
        }

        cargar("/Trabajadores/PlantasDeEmpresa?empresaId=" + encodeURIComponent(empresa.value), planta, "Todas");
    });

    planta.addEventListener("change", function () {
        if (!planta.value) {
            repoblar(centro, [], "Todos");
            repoblar(centroCosto, [], "Todos");
            return;
        }

        cargar("/Trabajadores/CentrosDePlanta?plantaId=" + encodeURIComponent(planta.value), centro, "Todos");
    });

    centro.addEventListener("change", function () {
        if (!centro.value) {
            repoblar(centroCosto, [], "Todos");
            return;
        }

        cargar("/Trabajadores/CentrosCostoDeCentro?centroId=" + encodeURIComponent(centro.value), centroCosto, "Todos");
    });
})();
