// Encadena los desplegables del formulario de personal:
//   empresa → planta → turnos habilitados
//   empresa → puesto
//   planta  → centro SAP → centro de costo
//
// El turno depende de la planta porque el turno especial de sábado solo existe en
// Planta 01, y esa restricción vive en el catálogo, no en el código. Centro depende
// de la planta, no directo de la empresa: en el formulario la secuencia obligatoria
// es Planta → Centro → Centro de costo, y el servidor resuelve la empresa dueña de
// esa planta (Planta.EmpresaId) para reusar la misma consulta de centros por
// empresa, sin pedirle al cliente que la recuerde.
//
// Los manejadores se enganchan desde aquí y no con atributos onclick en el HTML:
// un nombre con apóstrofo dentro de un atributo rompe el marcado y abre XSS.

(function () {
    "use strict";

    var empresa = document.getElementById("EmpresaId");
    var planta = document.getElementById("PlantaId");
    var turno = document.getElementById("TurnoId");
    var centro = document.getElementById("CentroId");
    var centroCosto = document.getElementById("CentroCostoId");
    var puesto = document.getElementById("PuestoId");
    var marcador = document.getElementById("IdMarcador");

    if (!empresa || !planta || !turno || !centro || !centroCosto || !puesto) {
        return;
    }

    // Los marcadores se identifican por su código en texto y no por el id de la fila del
    // catálogo: es el texto lo que se guarda en la asignación y lo que cruza con el
    // archivo del marcador. Por eso su desplegable lee otra propiedad del JSON.
    function repoblar(select, opciones, textoVacio, clave) {
        select.innerHTML = "";

        var vacia = document.createElement("option");
        vacia.value = "";
        vacia.textContent = textoVacio;
        select.appendChild(vacia);

        opciones.forEach(function (opcion) {
            var elemento = document.createElement("option");
            elemento.value = opcion[clave || "id"];
            // textContent, nunca innerHTML: el nombre viene de la base de datos.
            elemento.textContent = opcion.nombre;
            select.appendChild(elemento);
        });
    }

    function cargar(url, destino, textoVacio, clave) {
        return fetch(url, { headers: { "Accept": "application/json" } })
            .then(function (respuesta) {
                if (!respuesta.ok) {
                    throw new Error("No se pudo cargar el catálogo.");
                }
                return respuesta.json();
            })
            .then(function (datos) {
                repoblar(destino, datos, textoVacio, clave);
            })
            .catch(function () {
                repoblar(destino, [], "No se pudo cargar", clave);
            });
    }

    empresa.addEventListener("change", function () {
        repoblar(centro, [], "Seleccione una planta…");
        repoblar(centroCosto, [], "Seleccione un centro…");

        if (!empresa.value) {
            repoblar(planta, [], "Seleccione una empresa…");
            repoblar(turno, [], "Sin turno asignado");
            repoblar(puesto, [], "Seleccione una empresa…");
            return;
        }

        cargar("/Trabajadores/PlantasDeEmpresa?empresaId=" + encodeURIComponent(empresa.value),
               planta,
               "Seleccione…");

        cargar("/Trabajadores/PuestosDeEmpresa?empresaId=" + encodeURIComponent(empresa.value),
               puesto,
               "Seleccione…");
    });

    planta.addEventListener("change", function () {
        repoblar(centroCosto, [], "Seleccione un centro…");

        if (!planta.value) {
            repoblar(turno, [], "Sin turno asignado");
            repoblar(centro, [], "Seleccione una planta…");

            if (marcador) {
                repoblar(marcador, [], "Seleccione una unidad…", "valor");
            }

            return;
        }

        cargar("/Trabajadores/TurnosDePlanta?plantaId=" + encodeURIComponent(planta.value),
               turno,
               "Sin turno asignado");

        cargar("/Trabajadores/CentrosDePlanta?plantaId=" + encodeURIComponent(planta.value),
               centro,
               "Seleccione…");

        // Cada marcador pertenece a una planta: al cambiarla, los que estaban ofrecidos
        // dejan de ser válidos y el servidor rechazaría el guardado.
        if (marcador) {
            cargar("/Trabajadores/MarcadoresDePlanta?plantaId=" + encodeURIComponent(planta.value),
                   marcador,
                   "Sin marcador asignado",
                   "valor");
        }
    });

    centro.addEventListener("change", function () {
        if (!centro.value) {
            repoblar(centroCosto, [], "Seleccione un centro…");
            return;
        }

        cargar("/Trabajadores/CentrosCostoDeCentro?centroId=" + encodeURIComponent(centro.value),
               centroCosto,
               "Seleccione…");
    });
})();
