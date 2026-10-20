// Componente global: convierte un <select> nativo en un desplegable con buscador en
// vivo, para catálogos largos (trabajadores, periodos, plantas...) donde desplazarse
// opción por opción es incómodo.
//
// Reglas de diseño, todas obligatorias:
//
//   1. El <select> real NUNCA se reemplaza. Se queda en el DOM con su id/name intactos,
//      invisible pero "renderizado" (opacity: 0 sobre el mismo cuadro que ocupa el
//      control visible, no display:none): así el navegador todavía puede mostrar su
//      aviso nativo de "complete este campo" en los <select required> (Reportes), algo
//      que con display:none se pierde en silencio. El binding de Razor (asp-for) y el
//      submit del formulario dependen de que ese elemento siga siendo el que se envía.
//   2. Elegir una opción del desplegable propio actualiza select.value y dispara un
//      evento "change" SINTÉTICO sobre el select real. Es obligatorio: trabajador-form.js,
//      panel-trabajador.js, usuario-form.js y solicitudes-crear.js escuchan "change"
//      sobre estos mismos selects para encadenar Empresa→Planta→Área→Sección y los avisos
//      de Solicitudes. Si no se dispara ese evento, esas cascadas se rompen en silencio.
//   3. Un <select> que una cascada AJAX repuebla (repoblar/repoblarOpciones) se detecta
//      solo, con un MutationObserver por select que vigila sus <option>. Ningún script de
//      cascada existente se toca ni necesita llamar nada de este archivo.
//   4. Un <select> agregado más tarde (el panel flotante de Personal llega completo por
//      fetch + innerHTML) también se detecta solo, con un MutationObserver global sobre
//      document.body. Por eso initAll() no hace falta llamarlo de nuevo tras abrir el panel.
//
// Los manejadores se enganchan desde aquí y no con atributos onclick en el HTML:
// un nombre con apóstrofo dentro de un atributo rompe el marcado y abre XSS. Por el mismo
// motivo, las opciones del desplegable se arman con textContent, nunca con innerHTML: el
// texto de cada opción viene de la base de datos.

(function () {
    "use strict";

    var CLASE_SELECT = "select-buscable";

    // Todos los desplegables ya inicializados, para poder cerrar "los demás" cuando se
    // abre uno nuevo (solo uno abierto a la vez) sin tener que recorrer todo el DOM.
    var contextos = [];

    var contadorId = 0;

    var observadorGlobalInstalado = false;

    function limpiarTexto(texto) {
        // Las opciones armadas en varias líneas de Razor (el periodo en Asistencia, por
        // ejemplo) traen saltos de línea y sangría dentro del texto: se colapsan a un
        // solo espacio para que el desplegable muestre una etiqueta legible.
        return (texto || "").replace(/\s+/g, " ").trim();
    }

    function textoDeOpcion(opcion) {
        return limpiarTexto(opcion.textContent);
    }

    // ---------------------------------------------------------------------------
    // Construcción del marcado propio
    // ---------------------------------------------------------------------------

    function crearElementos() {
        contadorId += 1;
        var idLista = "select-buscable-lista-" + contadorId;

        var envoltorio = document.createElement("div");
        envoltorio.className = "select-buscable-envoltorio";

        var control = document.createElement("button");
        control.type = "button";
        control.className = "select-buscable-control";
        control.setAttribute("aria-haspopup", "listbox");
        control.setAttribute("aria-expanded", "false");

        var etiqueta = document.createElement("span");
        etiqueta.className = "select-buscable-etiqueta";

        var flecha = document.createElement("span");
        flecha.className = "select-buscable-flecha";
        flecha.setAttribute("aria-hidden", "true");
        flecha.textContent = "▾";

        control.appendChild(etiqueta);
        control.appendChild(flecha);

        var panel = document.createElement("div");
        panel.className = "select-buscable-panel";
        panel.hidden = true;

        var buscador = document.createElement("input");
        buscador.type = "text";
        buscador.className = "select-buscable-buscador";
        buscador.autocomplete = "off";
        buscador.spellcheck = false;
        buscador.setAttribute("role", "combobox");
        buscador.setAttribute("aria-expanded", "false");
        buscador.setAttribute("aria-controls", idLista);
        buscador.placeholder = "Buscar…";

        var lista = document.createElement("ul");
        lista.className = "select-buscable-lista";
        lista.id = idLista;
        lista.setAttribute("role", "listbox");

        var vacio = document.createElement("div");
        vacio.className = "select-buscable-vacio";
        vacio.textContent = "Sin resultados";
        vacio.hidden = true;

        panel.appendChild(buscador);
        panel.appendChild(lista);
        panel.appendChild(vacio);

        envoltorio.appendChild(control);
        envoltorio.appendChild(panel);

        return {
            idBase: "select-buscable-" + contadorId,
            envoltorio: envoltorio,
            control: control,
            etiqueta: etiqueta,
            panel: panel,
            buscador: buscador,
            lista: lista,
            vacio: vacio
        };
    }

    // Etiqueta del <optgroup> que contiene la opción, o cadena vacía si está suelta.
    function grupoDeOpcion(opcion) {
        return opcion.parentElement && opcion.parentElement.tagName === "OPTGROUP"
            ? limpiarTexto(opcion.parentElement.label)
            : "";
    }

    function construirOpciones(contexto) {
        contexto.lista.innerHTML = "";
        contexto.opciones = [];
        contexto.resaltadaIndice = -1;

        var grupoActual = null;
        var cabeceraActual = null;

        Array.prototype.forEach.call(contexto.select.options, function (opcion, indice) {
            var grupo = grupoDeOpcion(opcion);

            // Cabecera de grupo. Sin esto el componente APLANABA los <optgroup>, y en el
            // marcador de personal eso dejaba dos entradas "21" indistinguibles en pantalla
            // —una por aparato, dos personas distintas— justo lo que el agrupado viene a
            // resolver. No es opción: no se elige ni se navega con el teclado.
            if (grupo !== grupoActual) {
                grupoActual = grupo;
                cabeceraActual = null;

                if (grupo !== "") {
                    cabeceraActual = document.createElement("li");
                    cabeceraActual.className = "select-buscable-grupo";
                    cabeceraActual.setAttribute("role", "presentation");
                    cabeceraActual.textContent = grupo;
                    contexto.lista.appendChild(cabeceraActual);
                }
            }

            var item = document.createElement("li");
            item.className = "select-buscable-opcion";
            item.id = contexto.idBase + "-opcion-" + indice;
            item.setAttribute("role", "option");
            item.setAttribute("data-indice", String(indice));
            // textContent, nunca innerHTML: el nombre de cada opción viene de la base de
            // datos (trabajador, planta, periodo...).
            item.textContent = textoDeOpcion(opcion);

            if (opcion.disabled) {
                item.classList.add("select-buscable-opcion-deshabilitada");
                item.setAttribute("aria-disabled", "true");
            }

            contexto.lista.appendChild(item);

            contexto.opciones.push({
                valor: opcion.value,
                // El texto de búsqueda incluye el grupo: teclear "M-Z" tiene que encontrar
                // los códigos de ese aparato, que es como el usuario los piensa.
                texto: grupo === ""
                    ? textoDeOpcion(opcion)
                    : textoDeOpcion(opcion) + " " + grupo,
                elemento: item,
                cabecera: cabeceraActual,
                deshabilitada: opcion.disabled
            });
        });
    }

    function sincronizarEtiqueta(contexto) {
        var opcion = contexto.select.options[contexto.select.selectedIndex];

        if (!opcion) {
            contexto.etiqueta.textContent = "Seleccione…";
            return;
        }

        var grupo = grupoDeOpcion(opcion);

        // Con el desplegable cerrado también hace falta el grupo: "21" a secas no dice de
        // qué aparato es, y es la única pista que queda a la vista al revisar la ficha.
        contexto.etiqueta.textContent = grupo === "" || opcion.value === ""
            ? textoDeOpcion(opcion)
            : textoDeOpcion(opcion) + " · " + grupo;
    }

    function sincronizarDeshabilitado(contexto) {
        contexto.control.disabled = contexto.select.disabled;
    }

    function marcarSeleccionActual(contexto) {
        var valorActual = contexto.select.value;

        contexto.opciones.forEach(function (item) {
            var esActual = item.valor === valorActual;
            item.elemento.classList.toggle("select-buscable-opcion-actual", esActual);
            item.elemento.setAttribute("aria-selected", esActual ? "true" : "false");
        });
    }

    // ---------------------------------------------------------------------------
    // Filtro y resaltado (teclado + mouse comparten el mismo índice resaltado)
    // ---------------------------------------------------------------------------

    function filtrar(contexto, texto) {
        var termino = limpiarTexto(texto).toLowerCase();
        var hayVisibles = false;

        // Las cabeceras de grupo se ocultan primero y se reabren solo si alguna de sus
        // opciones sobrevive al filtro: si no, al buscar "22" quedaría en pantalla el
        // rótulo de un aparato sin una sola fila debajo.
        contexto.opciones.forEach(function (item) {
            if (item.cabecera) {
                item.cabecera.hidden = true;
            }
        });

        contexto.opciones.forEach(function (item) {
            var coincide = termino === "" || item.texto.toLowerCase().indexOf(termino) >= 0;
            item.elemento.hidden = !coincide;

            if (coincide) {
                hayVisibles = true;

                if (item.cabecera) {
                    item.cabecera.hidden = false;
                }
            }
        });

        contexto.vacio.hidden = hayVisibles;
        resaltar(contexto, -1);
    }

    function primerVisible(contexto) {
        for (var i = 0; i < contexto.opciones.length; i++) {
            var item = contexto.opciones[i];
            if (!item.elemento.hidden && !item.deshabilitada) {
                return i;
            }
        }

        return -1;
    }

    function siguienteVisible(contexto, desde, direccion) {
        var total = contexto.opciones.length;
        var indice = desde;

        if (desde === -1) {
            indice = direccion > 0 ? -1 : total;
        }

        for (var i = 0; i < total; i++) {
            indice += direccion;

            if (indice < 0 || indice >= total) {
                return desde;
            }

            var item = contexto.opciones[indice];
            if (!item.elemento.hidden && !item.deshabilitada) {
                return indice;
            }
        }

        return desde;
    }

    function resaltar(contexto, indice) {
        if (contexto.resaltadaIndice >= 0 && contexto.opciones[contexto.resaltadaIndice]) {
            contexto.opciones[contexto.resaltadaIndice].elemento.classList.remove("resaltada");
        }

        contexto.resaltadaIndice = indice;

        if (indice >= 0) {
            var item = contexto.opciones[indice];
            item.elemento.classList.add("resaltada");
            item.elemento.scrollIntoView({ block: "nearest" });
            contexto.buscador.setAttribute("aria-activedescendant", item.elemento.id);
        } else {
            contexto.buscador.removeAttribute("aria-activedescendant");
        }
    }

    // ---------------------------------------------------------------------------
    // Abrir / cerrar / elegir
    // ---------------------------------------------------------------------------

    function estaAbierto(contexto) {
        return !contexto.panel.hidden;
    }

    function cerrarTodos() {
        contextos.forEach(function (otro) {
            if (estaAbierto(otro)) {
                cerrar(otro);
            }
        });
    }

    function abrir(contexto) {
        if (contexto.select.disabled) {
            return;
        }

        cerrarTodos();

        contexto.panel.hidden = false;
        contexto.envoltorio.classList.add("abierto");
        contexto.control.setAttribute("aria-expanded", "true");
        contexto.buscador.setAttribute("aria-expanded", "true");

        contexto.buscador.value = "";
        filtrar(contexto, "");
        marcarSeleccionActual(contexto);

        var indiceActual = -1;
        for (var i = 0; i < contexto.opciones.length; i++) {
            if (contexto.opciones[i].valor === contexto.select.value) {
                indiceActual = i;
                break;
            }
        }
        resaltar(contexto, indiceActual >= 0 ? indiceActual : primerVisible(contexto));

        // Con un setTimeout(0): si el navegador todavía no terminó de pintar el panel
        // como visible (venía de hidden = true), algunos motores ignoran el focus().
        window.setTimeout(function () {
            contexto.buscador.focus();
        }, 0);
    }

    function cerrar(contexto) {
        contexto.panel.hidden = true;
        contexto.envoltorio.classList.remove("abierto");
        contexto.control.setAttribute("aria-expanded", "false");
        contexto.buscador.setAttribute("aria-expanded", "false");
    }

    function elegir(contexto, indice) {
        var item = contexto.opciones[indice];

        if (!item || item.deshabilitada) {
            return;
        }

        contexto.select.value = item.valor;

        // Sintético y con bubbles: es lo que escuchan las cascadas existentes
        // (trabajador-form.js, panel-trabajador.js, usuario-form.js, solicitudes-crear.js).
        contexto.select.dispatchEvent(new Event("change", { bubbles: true }));

        cerrar(contexto);
        contexto.control.focus();
    }

    // ---------------------------------------------------------------------------
    // Eventos de un desplegable ya construido
    // ---------------------------------------------------------------------------

    function conectarEventos(contexto) {
        contexto.control.addEventListener("click", function () {
            if (estaAbierto(contexto)) {
                cerrar(contexto);
            } else {
                abrir(contexto);
            }
        });

        // Solo ↓ abre desde el control: Enter y espacio ya activan el <button> nativo y
        // dispararían el "click" de arriba también, abriendo y cerrando a la vez.
        contexto.control.addEventListener("keydown", function (evento) {
            if (evento.key === "ArrowDown" && !estaAbierto(contexto)) {
                evento.preventDefault();
                abrir(contexto);
            }
        });

        contexto.buscador.addEventListener("input", function () {
            filtrar(contexto, contexto.buscador.value);
        });

        contexto.buscador.addEventListener("keydown", function (evento) {
            if (evento.key === "ArrowDown") {
                evento.preventDefault();
                resaltar(contexto, siguienteVisible(contexto, contexto.resaltadaIndice, 1));
                return;
            }

            if (evento.key === "ArrowUp") {
                evento.preventDefault();
                resaltar(contexto, siguienteVisible(contexto, contexto.resaltadaIndice, -1));
                return;
            }

            if (evento.key === "Enter") {
                evento.preventDefault();
                if (contexto.resaltadaIndice >= 0) {
                    elegir(contexto, contexto.resaltadaIndice);
                }
                return;
            }

            if (evento.key === "Escape") {
                evento.preventDefault();
                cerrar(contexto);
                contexto.control.focus();
            }
        });

        // mousedown y no click: si se esperara al click, el buscador pierde el foco
        // primero (blur) y el cierre por foco perdido llegaría antes que la selección.
        contexto.lista.addEventListener("mousedown", function (evento) {
            var li = evento.target.closest("[data-indice]");

            if (!li) {
                return;
            }

            evento.preventDefault();
            elegir(contexto, parseInt(li.getAttribute("data-indice"), 10));
        });

        contexto.lista.addEventListener("mouseover", function (evento) {
            var li = evento.target.closest("[data-indice]");

            if (!li) {
                return;
            }

            var indice = parseInt(li.getAttribute("data-indice"), 10);
            if (!contexto.opciones[indice].deshabilitada) {
                resaltar(contexto, indice);
            }
        });

        // Cierra al salir por Tab. El clic afuera ya lo cubre el listener global de
        // document (más abajo): este además atrapa la navegación por teclado.
        contexto.envoltorio.addEventListener("focusout", function () {
            window.setTimeout(function () {
                if (!contexto.envoltorio.contains(document.activeElement)) {
                    cerrar(contexto);
                }
            }, 0);
        });

        // El select real puede cambiar de valor por una vía distinta a elegir(): otro
        // script que lo repueble y deje un value por defecto, o el propio navegador si
        // el usuario llegó a abrir el <select> nativo (sigue existiendo, solo que
        // invisible). Escuchar "change" aquí, y no duplicar la sincronización en elegir(),
        // mantiene una sola fuente de verdad para lo que se muestra en el control.
        contexto.select.addEventListener("change", function () {
            sincronizarEtiqueta(contexto);
            marcarSeleccionActual(contexto);
        });
    }

    // ---------------------------------------------------------------------------
    // Alta / actualización de un select
    // ---------------------------------------------------------------------------

    function actualizar(contexto) {
        construirOpciones(contexto);
        sincronizarEtiqueta(contexto);
        sincronizarDeshabilitado(contexto);
        marcarSeleccionActual(contexto);

        if (estaAbierto(contexto)) {
            filtrar(contexto, contexto.buscador.value);
        }
    }

    function inicializarSelect(select) {
        if (select.dataset.selectBuscableListo === "1") {
            return;
        }
        select.dataset.selectBuscableListo = "1";

        // Fuera del recorrido con Tab: el usuario navega por el control visible, nunca
        // por el select oculto. Sigue siendo enfocable por script (el navegador lo hace
        // así al mostrar el aviso nativo de "complete este campo" en un <select required>).
        select.tabIndex = -1;

        var elementos = crearElementos();

        var contexto = {
            select: select,
            idBase: elementos.idBase,
            envoltorio: elementos.envoltorio,
            control: elementos.control,
            etiqueta: elementos.etiqueta,
            panel: elementos.panel,
            buscador: elementos.buscador,
            lista: elementos.lista,
            vacio: elementos.vacio,
            opciones: [],
            resaltadaIndice: -1
        };

        select.parentNode.insertBefore(contexto.envoltorio, select);
        // El select queda como primer hijo del envoltorio: sigue "renderizado" (no
        // display:none) para que el aviso nativo de validación pueda anclarse a él,
        // pero superpuesto e invisible sobre el control propio (ver searchable-select.css).
        contexto.envoltorio.insertBefore(select, contexto.control);

        actualizar(contexto);
        conectarEventos(contexto);

        var observadorSelect = new MutationObserver(function () {
            actualizar(contexto);
        });
        observadorSelect.observe(select, { childList: true, attributes: true, attributeFilter: ["disabled"] });

        select.searchableSelectContexto = contexto;
        contextos.push(contexto);
    }

    // ---------------------------------------------------------------------------
    // Detección de selects nuevos insertados por AJAX (panel flotante de Personal y
    // cualquier otro bloque futuro), sin tocar el script que los inserta.
    // ---------------------------------------------------------------------------

    function buscarSelectsEn(nodo) {
        var encontrados = [];

        if (nodo.nodeType !== 1) {
            return encontrados;
        }

        if (nodo.matches && nodo.matches("select." + CLASE_SELECT)) {
            encontrados.push(nodo);
        }

        if (nodo.querySelectorAll) {
            Array.prototype.forEach.call(
                nodo.querySelectorAll("select." + CLASE_SELECT),
                function (select) { encontrados.push(select); });
        }

        return encontrados;
    }

    function instalarObservadorGlobal() {
        if (observadorGlobalInstalado) {
            return;
        }
        observadorGlobalInstalado = true;

        var observador = new MutationObserver(function (mutaciones) {
            mutaciones.forEach(function (mutacion) {
                Array.prototype.forEach.call(mutacion.addedNodes, function (nodo) {
                    buscarSelectsEn(nodo).forEach(inicializarSelect);
                });
            });
        });

        observador.observe(document.body, { childList: true, subtree: true });
    }

    // ---------------------------------------------------------------------------
    // API pública
    // ---------------------------------------------------------------------------

    function initAll() {
        instalarObservadorGlobal();

        Array.prototype.forEach.call(
            document.querySelectorAll("select." + CLASE_SELECT),
            inicializarSelect);
    }

    function refresh(select) {
        if (!select) {
            return;
        }

        if (select.searchableSelectContexto) {
            actualizar(select.searchableSelectContexto);
            return;
        }

        inicializarSelect(select);
    }

    document.addEventListener("mousedown", function (evento) {
        contextos.forEach(function (contexto) {
            if (estaAbierto(contexto) && !contexto.envoltorio.contains(evento.target)) {
                cerrar(contexto);
            }
        });
    });

    window.SearchableSelect = {
        initAll: initAll,
        refresh: refresh
    };
})();
