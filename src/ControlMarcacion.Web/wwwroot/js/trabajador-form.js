// Encadena los desplegables del formulario de personal:
//   empresa → planta → turnos habilitados
//   empresa → puesto
//   planta  → centro SAP → centro de costo
//   planta  → aparato marcador
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
    var aparato = document.getElementById("EquipoMarcadorId");
    var numero = document.getElementById("IdMarcador");

    if (!empresa || !planta || !turno || !centro || !centroCosto || !puesto) {
        return;
    }

    // Los aparatos de la planta. Con uno solo se deja elegido y sin opción vacía: no hay
    // nada que preguntar, y se cargan de una vez sus números.
    function repoblarAparatos(opciones, textoVacio) {
        if (!aparato) {
            return;
        }

        aparato.innerHTML = "";

        if (opciones.length !== 1) {
            var vacia = document.createElement("option");
            vacia.value = "";
            vacia.textContent = opciones.length === 0 ? textoVacio : "Seleccione el aparato…";
            aparato.appendChild(vacia);
        }

        opciones.forEach(function (opcion) {
            var elemento = document.createElement("option");
            elemento.value = opcion.id;
            // textContent, nunca innerHTML: el nombre viene de la base de datos.
            elemento.textContent = opcion.nombre + " (" + opcion.codigo + ")";
            aparato.appendChild(elemento);
        });

        if (opciones.length === 1) {
            cargarNumeros(opciones[0].id);
        } else {
            repoblarNumeros([], opciones.length === 0 ? textoVacio : "Elija un marcador…");
        }
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

    // Los números registrados del marcador elegido, con quién tiene hoy cada uno. Solo se
    // elige entre estos: los números se dan de alta a mano en Marcadores.
    function repoblarNumeros(opciones, textoVacio) {
        if (!numero) {
            return;
        }

        numero.innerHTML = "";

        var vacia = document.createElement("option");
        vacia.value = "";
        vacia.textContent = opciones.length === 0 ? textoVacio : "Sin número asignado";
        numero.appendChild(vacia);

        opciones.forEach(function (opcion) {
            var elemento = document.createElement("option");
            elemento.value = opcion.codigo;
            elemento.textContent = opcion.codigo + " · " + (opcion.asignadoA || "libre");
            numero.appendChild(elemento);
        });
    }

    function cargarNumeros(equipoId) {
        if (!equipoId) {
            repoblarNumeros([], "Elija un marcador…");
            return;
        }

        pedir("/Trabajadores/NumerosDeEquipo?equipoId=" + encodeURIComponent(equipoId))
            .then(function (datos) {
                repoblarNumeros(datos, "El marcador no tiene números registrados");
            })
            .catch(function () {
                repoblarNumeros([], "No se pudo cargar");
            });
    }

    function pedir(url) {
        return fetch(url, { headers: { "Accept": "application/json" } })
            .then(function (respuesta) {
                if (!respuesta.ok) {
                    throw new Error("No se pudo cargar el catálogo.");
                }
                return respuesta.json();
            });
    }

    function cargar(url, destino, textoVacio) {
        return pedir(url)
            .then(function (datos) {
                repoblar(destino, datos, textoVacio);
            })
            .catch(function () {
                repoblar(destino, [], "No se pudo cargar");
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

            repoblarAparatos([], "Seleccione una unidad…");

            return;
        }

        cargar("/Trabajadores/TurnosDePlanta?plantaId=" + encodeURIComponent(planta.value),
               turno,
               "Sin turno asignado");

        cargar("/Trabajadores/CentrosDePlanta?plantaId=" + encodeURIComponent(planta.value),
               centro,
               "Seleccione…");

        // Cada aparato pertenece a una planta: al cambiarla, los que estaban ofrecidos
        // dejan de ser válidos y el servidor rechazaría el guardado.
        pedir("/Trabajadores/EquiposDePlanta?plantaId=" + encodeURIComponent(planta.value))
            .then(function (datos) {
                repoblarAparatos(datos, "La unidad no tiene aparatos");
            })
            .catch(function () {
                repoblarAparatos([], "No se pudo cargar");
            });
    });

    if (aparato) {
        aparato.addEventListener("change", function () {
            cargarNumeros(aparato.value);
        });
    }

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
