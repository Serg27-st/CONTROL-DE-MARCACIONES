// Panel flotante lateral de Personal: abrir/cerrar sin navegar, cascada de
// desplegables dentro del panel (reusa los mismos endpoints que trabajador-form.js),
// búsqueda rápida con debounce y guardado por AJAX.
//
// Los manejadores se enganchan por delegación de eventos sobre "document": el
// contenido del panel se reemplaza por completo en cada apertura, así que atar
// listeners a nodos concretos los perdería en la siguiente carga.

(function () {
    "use strict";

    var CLASE_ABIERTO = "abierto";

    var contenedor = document.getElementById("panel-trabajador-contenedor");

    if (!contenedor) {
        return;
    }

    // Contador de peticiones en vuelo: si el usuario abre otro trabajador antes de
    // que responda el primero, la respuesta vieja se descarta en vez de pisar el
    // contenido del que se está mostrando ahora.
    var ultimaPeticion = 0;

    function estaAbierto() {
        return !!contenedor.querySelector(".panel-flotante");
    }

    function plantillaCargando() {
        return (
            '<div class="panel-overlay ' + CLASE_ABIERTO + '" data-panel-overlay></div>' +
            '<aside class="panel-flotante ' + CLASE_ABIERTO + '">' +
            '<header class="panel-flotante-cabecera">' +
            '<h2>Información del colaborador</h2>' +
            '<button type="button" class="panel-flotante-cerrar" data-panel-cerrar aria-label="Cerrar panel">&times;</button>' +
            '</header>' +
            '<div class="panel-flotante-contenido">' +
            '<div class="panel-cargando"><span class="panel-spinner" aria-hidden="true"></span> Cargando…</div>' +
            '</div>' +
            '</aside>'
        );
    }

    function abrirPanel(id, modo) {
        ultimaPeticion += 1;
        var peticionActual = ultimaPeticion;

        if (!estaAbierto()) {
            contenedor.innerHTML = plantillaCargando();
        } else {
            var contenido = contenedor.querySelector(".panel-flotante-contenido");
            if (contenido) {
                contenido.innerHTML = '<div class="panel-cargando"><span class="panel-spinner" aria-hidden="true"></span> Cargando…</div>';
            }
        }

        var url = "/Trabajadores/Panel?id=" + encodeURIComponent(id) + "&modo=" + encodeURIComponent(modo || "ver");

        fetch(url, { headers: { "Accept": "text/html" } })
            .then(function (respuesta) {
                if (!respuesta.ok) {
                    throw new Error("No se pudo cargar la información del trabajador.");
                }
                return respuesta.text();
            })
            .then(function (html) {
                if (peticionActual !== ultimaPeticion) {
                    return;
                }

                contenedor.innerHTML = html;

                var overlay = contenedor.querySelector(".panel-overlay");
                var panel = contenedor.querySelector(".panel-flotante");

                if (overlay) {
                    overlay.classList.add(CLASE_ABIERTO);
                }
                if (panel) {
                    panel.classList.add(CLASE_ABIERTO);
                }
            })
            .catch(function () {
                if (peticionActual !== ultimaPeticion) {
                    return;
                }

                var contenido = contenedor.querySelector(".panel-flotante-contenido");
                if (contenido) {
                    contenido.innerHTML = '<div class="aviso aviso-error" role="alert">No se pudo cargar la información del trabajador.</div>';
                }
            });
    }

    function cerrarPanel() {
        ultimaPeticion += 1;

        var overlay = contenedor.querySelector(".panel-overlay");
        var panel = contenedor.querySelector(".panel-flotante");

        if (!panel) {
            contenedor.innerHTML = "";
            return;
        }

        if (overlay) {
            overlay.classList.remove(CLASE_ABIERTO);
        }
        panel.classList.remove(CLASE_ABIERTO);

        window.setTimeout(function () {
            contenedor.innerHTML = "";
        }, 320);
    }

    function cambiarModo(modo) {
        var bloqueVer = contenedor.querySelector("[data-bloque-ver]");
        var bloqueEditar = contenedor.querySelector("[data-bloque-editar]");

        if (!bloqueVer || !bloqueEditar) {
            return;
        }

        if (modo === "editar") {
            bloqueVer.classList.add("oculto");
            bloqueEditar.classList.remove("oculto");
        } else {
            bloqueEditar.classList.add("oculto");
            bloqueVer.classList.remove("oculto");
        }
    }

    function mostrarToast(mensaje) {
        var toast = document.createElement("div");
        toast.className = "panel-toast";
        toast.setAttribute("role", "status");
        toast.textContent = mensaje;
        document.body.appendChild(toast);

        window.requestAnimationFrame(function () {
            toast.classList.add("visible");
        });

        window.setTimeout(function () {
            toast.classList.remove("visible");
            window.setTimeout(function () {
                toast.remove();
            }, 300);
        }, 3000);
    }

    // Después de guardar, se vuelve a pedir la página actual (con los mismos filtros
    // de la URL) y se reemplaza solo el bloque de resultados. Así la tabla siempre
    // queda igual a como la habría renderizado el servidor, sin duplicar en JavaScript
    // el marcado de cada fila.
    function recargarTabla() {
        var actual = document.getElementById("resultados-personal");

        if (!actual) {
            return;
        }

        fetch(window.location.href, { headers: { "X-Requested-With": "XMLHttpRequest" } })
            .then(function (respuesta) {
                return respuesta.text();
            })
            .then(function (html) {
                var documento = new DOMParser().parseFromString(html, "text/html");
                var nuevo = documento.getElementById("resultados-personal");

                if (nuevo) {
                    actual.replaceWith(nuevo);
                }
            })
            .catch(function () {
                // Si falla, el usuario sigue viendo la tabla anterior: no es peor que
                // antes de guardar, y el dato ya quedó persistido en el servidor.
            });
    }

    function enviarFormulario(formulario) {
        var mensaje = formulario.querySelector("[data-panel-mensaje]");
        var boton = formulario.querySelector("button[type=submit]");

        if (mensaje) {
            mensaje.classList.add("oculto");
            mensaje.textContent = "";
        }

        if (boton) {
            boton.disabled = true;
        }

        fetch(formulario.action, {
            method: "POST",
            headers: { "X-Requested-With": "XMLHttpRequest" },
            body: new FormData(formulario)
        })
            .then(function (respuesta) {
                return respuesta.json();
            })
            .then(function (resultado) {
                if (resultado && resultado.ok) {
                    cerrarPanel();
                    mostrarToast(resultado.mensaje || "Cambios guardados correctamente.");
                    recargarTabla();
                    return;
                }

                if (mensaje) {
                    mensaje.textContent = (resultado && resultado.mensaje) || "No se pudo guardar.";
                    mensaje.classList.remove("oculto");
                }
            })
            .catch(function () {
                if (mensaje) {
                    mensaje.textContent = "No se pudo conectar con el servidor. Intente nuevamente.";
                    mensaje.classList.remove("oculto");
                }
            })
            .finally(function () {
                if (boton) {
                    boton.disabled = false;
                }
            });
    }

    document.addEventListener("click", function (evento) {
        var abrir = evento.target.closest("[data-panel-abrir]");
        if (abrir) {
            abrirPanel(abrir.getAttribute("data-panel-id"), abrir.getAttribute("data-panel-modo"));
            return;
        }

        if (evento.target.closest("[data-panel-cerrar]") ||
            evento.target.closest("[data-panel-overlay]") ||
            evento.target.closest("[data-panel-cancelar]")) {
            cerrarPanel();
            return;
        }

        var cambiar = evento.target.closest("[data-panel-cambiar-modo]");
        if (cambiar) {
            cambiarModo(cambiar.getAttribute("data-panel-cambiar-modo"));
        }
    });

    document.addEventListener("submit", function (evento) {
        var formulario = evento.target.closest("[data-panel-formulario]");
        if (!formulario) {
            return;
        }

        evento.preventDefault();
        enviarFormulario(formulario);
    });

    document.addEventListener("keydown", function (evento) {
        if (evento.key === "Escape" && estaAbierto()) {
            cerrarPanel();
        }
    });

    // ---- Cascada dentro del panel: Empresa → Planta → Turnos, Empresa → Puesto, ----
    // ---- Planta → Centro → Centro de costo ----
    // Reusa los mismos endpoints JSON que ya usa trabajador-form.js para el formulario
    // de página completa (PlantasDeEmpresa / TurnosDePlanta / PuestosDeEmpresa /
    // CentrosDePlanta / CentrosCostoDeCentro); el cableado es propio porque los campos
    // del panel se insertan dinámicamente y usan ids distintos para no chocar con los
    // filtros de la tabla.

    function repoblarOpciones(select, opciones, textoVacio) {
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

    // Los aparatos de la planta. Con uno solo se deja elegido y sin opción vacía: el
    // número que se escriba al lado es de ese aparato. Ver Editar.cshtml.
    function repoblarAparatos(select, opciones, textoVacio) {
        select.innerHTML = "";

        if (opciones.length !== 1) {
            var vacia = document.createElement("option");
            vacia.value = "";
            vacia.textContent = opciones.length === 0 ? textoVacio : "Seleccione el aparato…";
            select.appendChild(vacia);
        }

        opciones.forEach(function (opcion) {
            var elemento = document.createElement("option");
            elemento.value = opcion.id;
            elemento.textContent = opcion.nombre + " (" + opcion.codigo + ")";
            select.appendChild(elemento);
        });
    }

    function pedirJson(url) {
        return fetch(url, { headers: { "Accept": "application/json" } })
            .then(function (respuesta) {
                if (!respuesta.ok) {
                    throw new Error("No se pudo cargar el catálogo.");
                }
                return respuesta.json();
            });
    }

    function cargarAparatos(url, destino) {
        return pedirJson(url)
            .then(function (datos) {
                repoblarAparatos(destino, datos, "La unidad no tiene aparatos");
            })
            .catch(function () {
                repoblarAparatos(destino, [], "No se pudo cargar");
            });
    }

    function cargarOpciones(url, destino, textoVacio) {
        return pedirJson(url)
            .then(function (datos) {
                repoblarOpciones(destino, datos, textoVacio);
            })
            .catch(function () {
                repoblarOpciones(destino, [], "No se pudo cargar");
            });
    }

    document.addEventListener("change", function (evento) {
        var campo = evento.target;

        // Delegado en `document` porque el panel se inyecta después de cargar la página:
        // un listener puesto sobre el select no sobreviviría al siguiente render.
        if (campo.id === "PanelEmpresaId") {
            var planta = document.getElementById("PanelPlantaId");
            var turno = document.getElementById("PanelTurnoId");
            var centro = document.getElementById("PanelCentroId");
            var centroCosto = document.getElementById("PanelCentroCostoId");
            var puesto = document.getElementById("PanelPuestoId");

            if (!planta || !turno || !centro || !centroCosto || !puesto) {
                return;
            }

            repoblarOpciones(centro, [], "Seleccione una planta…");
            repoblarOpciones(centroCosto, [], "Seleccione un centro…");

            if (!campo.value) {
                repoblarOpciones(planta, [], "Seleccione una empresa…");
                repoblarOpciones(turno, [], "Sin turno asignado");
                repoblarOpciones(puesto, [], "Seleccione una empresa…");
                return;
            }

            cargarOpciones(
                "/Trabajadores/PlantasDeEmpresa?empresaId=" + encodeURIComponent(campo.value),
                planta,
                "Seleccione…");

            cargarOpciones(
                "/Trabajadores/PuestosDeEmpresa?empresaId=" + encodeURIComponent(campo.value),
                puesto,
                "Seleccione…");
            return;
        }

        if (campo.id === "PanelPlantaId") {
            var turnoDestino = document.getElementById("PanelTurnoId");
            var centroDestino = document.getElementById("PanelCentroId");
            var centroCostoDestino = document.getElementById("PanelCentroCostoId");
            var aparatoDestino = document.getElementById("PanelEquipoMarcadorId");

            if (!turnoDestino || !centroDestino || !centroCostoDestino) {
                return;
            }

            repoblarOpciones(centroCostoDestino, [], "Seleccione un centro…");

            if (!campo.value) {
                repoblarOpciones(turnoDestino, [], "Sin turno asignado");
                repoblarOpciones(centroDestino, [], "Seleccione una planta…");

                if (aparatoDestino) {
                    repoblarAparatos(aparatoDestino, [], "Seleccione una unidad…");
                }

                return;
            }

            // Cada aparato pertenece a una planta: al cambiarla, los que estaban
            // ofrecidos dejan de ser válidos y el servidor rechazaría el guardado.
            if (aparatoDestino) {
                cargarAparatos(
                    "/Trabajadores/EquiposDePlanta?plantaId=" + encodeURIComponent(campo.value),
                    aparatoDestino);
            }

            cargarOpciones(
                "/Trabajadores/TurnosDePlanta?plantaId=" + encodeURIComponent(campo.value),
                turnoDestino,
                "Sin turno asignado");

            cargarOpciones(
                "/Trabajadores/CentrosDePlanta?plantaId=" + encodeURIComponent(campo.value),
                centroDestino,
                "Seleccione…");
            return;
        }

        if (campo.id === "PanelCentroId") {
            var centroCostoDestino = document.getElementById("PanelCentroCostoId");
            if (!centroCostoDestino) {
                return;
            }

            if (!campo.value) {
                repoblarOpciones(centroCostoDestino, [], "Seleccione un centro…");
                return;
            }

            cargarOpciones(
                "/Trabajadores/CentrosCostoDeCentro?centroId=" + encodeURIComponent(campo.value),
                centroCostoDestino,
                "Seleccione…");
        }
    });

    // ---- Búsqueda rápida (Trabajadores/Index) ----

    var entradaBusqueda = document.getElementById("BuscarRapidoInput");
    var resultadosBusqueda = document.getElementById("BuscarRapidoResultados");
    var temporizadorBusqueda = null;

    function ocultarResultadosBusqueda() {
        if (resultadosBusqueda) {
            resultadosBusqueda.hidden = true;
        }
    }

    function crearItemBusqueda(t) {
        var item = document.createElement("button");
        item.type = "button";
        item.className = "busqueda-rapida-item";
        item.setAttribute("data-panel-abrir", "");
        item.setAttribute("data-panel-id", t.id);
        item.setAttribute("data-panel-modo", "ver");

        var titulo = document.createElement("span");
        titulo.className = "busqueda-rapida-titulo";
        titulo.textContent = t.apellidosYNombres;

        var detalleTexto = t.tipoDocumento + " " + t.numeroDocumento +
            " · " + t.empresa + " / " + t.planta +
            (t.idMarcador
                ? " · Marcador " + t.idMarcador + (t.equipo ? " (" + t.equipo + ")" : "")
                : "") +
            (t.estaActivo ? "" : " · Cesado");

        var detalleSpan = document.createElement("span");
        detalleSpan.className = "busqueda-rapida-detalle";
        detalleSpan.textContent = detalleTexto;

        item.appendChild(titulo);
        item.appendChild(detalleSpan);

        return item;
    }

    function mostrarResultadosBusqueda(resultados) {
        if (!resultadosBusqueda) {
            return;
        }

        resultadosBusqueda.innerHTML = "";

        if (!resultados || resultados.length === 0) {
            var vacio = document.createElement("div");
            vacio.className = "busqueda-rapida-item busqueda-rapida-vacio";
            vacio.textContent = "Sin resultados.";
            resultadosBusqueda.appendChild(vacio);
            resultadosBusqueda.hidden = false;
            return;
        }

        resultados.forEach(function (t) {
            resultadosBusqueda.appendChild(crearItemBusqueda(t));
        });

        resultadosBusqueda.hidden = false;
    }

    function buscarRapido(texto) {
        fetch("/Trabajadores/BuscarRapido?texto=" + encodeURIComponent(texto), { headers: { "Accept": "application/json" } })
            .then(function (respuesta) {
                if (!respuesta.ok) {
                    throw new Error("Error de búsqueda.");
                }
                return respuesta.json();
            })
            .then(function (resultados) {
                mostrarResultadosBusqueda(resultados);
            })
            .catch(function () {
                if (!resultadosBusqueda) {
                    return;
                }
                resultadosBusqueda.innerHTML = '<div class="busqueda-rapida-item busqueda-rapida-vacio">No se pudo completar la búsqueda.</div>';
                resultadosBusqueda.hidden = false;
            });
    }

    if (entradaBusqueda && resultadosBusqueda) {
        entradaBusqueda.addEventListener("input", function () {
            var texto = entradaBusqueda.value.trim();

            window.clearTimeout(temporizadorBusqueda);

            if (!texto) {
                ocultarResultadosBusqueda();
                resultadosBusqueda.innerHTML = "";
                return;
            }

            temporizadorBusqueda = window.setTimeout(function () {
                buscarRapido(texto);
            }, 300);
        });

        entradaBusqueda.addEventListener("focus", function () {
            if (resultadosBusqueda.innerHTML.trim()) {
                resultadosBusqueda.hidden = false;
            }
        });

        document.addEventListener("click", function (evento) {
            var dentroDeBusqueda = evento.target === entradaBusqueda || resultadosBusqueda.contains(evento.target);

            if (!dentroDeBusqueda) {
                ocultarResultadosBusqueda();
            }

            if (evento.target.closest(".busqueda-rapida-item[data-panel-abrir]")) {
                ocultarResultadosBusqueda();
                entradaBusqueda.value = "";
            }
        });
    }
})();
