// Zona de arrastrar y soltar de la carga de marcaciones.
//
// Es una mejora sobre un formulario que ya funciona sin JavaScript: el <input type="file">
// vive dentro de un <label>, así que la zona es clicable y operable con teclado aunque
// este archivo no llegue a cargarse. Lo que se agrega aquí es soltar el archivo encima,
// ver cuál quedó elegido y avisar de la extensión y el tamaño ANTES de subir 20 MB.
//
// Nada de lo que se valida aquí es un control: el servidor vuelve a comprobar tipo y
// tamaño en MarcacionesController.Cargar. Esto solo ahorra el viaje.

(function () {
    "use strict";

    var EXTENSIONES = [".txt", ".dat"];
    var TAMANIO_MAXIMO = 20 * 1024 * 1024;

    document.addEventListener("DOMContentLoaded", function () {
        var zona = document.getElementById("zona-soltar");
        var entrada = document.getElementById("archivo");
        var etiqueta = document.getElementById("archivo-elegido");

        if (!zona || !entrada || !etiqueta) {
            return;
        }

        function extensionDe(nombre) {
            var punto = nombre.lastIndexOf(".");

            return punto < 0 ? "" : nombre.substring(punto).toLowerCase();
        }

        function tamanioLegible(bytes) {
            if (bytes < 1024) {
                return bytes + " B";
            }

            if (bytes < 1024 * 1024) {
                return (bytes / 1024).toFixed(1) + " KB";
            }

            return (bytes / 1024 / 1024).toFixed(1) + " MB";
        }

        function describir() {
            var archivo = entrada.files && entrada.files[0];

            zona.classList.remove("arrastrando");

            if (!archivo) {
                etiqueta.hidden = true;
                etiqueta.textContent = "";
                etiqueta.classList.remove("invalido");
                zona.classList.remove("con-archivo");
                return;
            }

            var extension = extensionDe(archivo.name);
            var problema = "";

            if (EXTENSIONES.indexOf(extension) < 0) {
                problema = "El marcador exporta un .txt (o .dat). Este archivo es «" +
                    (extension || "sin extensión") + "»: revise que sea el correcto.";
            } else if (archivo.size > TAMANIO_MAXIMO) {
                problema = "Pesa " + tamanioLegible(archivo.size) + " y el máximo son 20 MB.";
            } else if (archivo.size === 0) {
                problema = "El archivo está vacío.";
            }

            etiqueta.hidden = false;
            etiqueta.textContent = problema
                ? archivo.name + " — " + problema
                : archivo.name + " · " + tamanioLegible(archivo.size);

            etiqueta.classList.toggle("invalido", problema !== "");
            zona.classList.toggle("con-archivo", problema === "");
        }

        entrada.addEventListener("change", describir);

        // Sin estos preventDefault el navegador abre el archivo soltado en una pestaña y
        // se pierde la página con el formulario a medio llenar.
        ["dragenter", "dragover", "dragleave", "drop"].forEach(function (evento) {
            zona.addEventListener(evento, function (e) {
                e.preventDefault();
                e.stopPropagation();
            });
        });

        ["dragenter", "dragover"].forEach(function (evento) {
            zona.addEventListener(evento, function () {
                zona.classList.add("arrastrando");
            });
        });

        zona.addEventListener("dragleave", function () {
            zona.classList.remove("arrastrando");
        });

        zona.addEventListener("drop", function (e) {
            var soltados = e.dataTransfer && e.dataTransfer.files;

            if (!soltados || soltados.length === 0) {
                zona.classList.remove("arrastrando");
                return;
            }

            // Se asigna el FileList completo al input para que el archivo viaje en el
            // envío normal del formulario. Solo se toma el primero: la pantalla carga un
            // archivo por vez y aceptar varios daría a entender que los procesa todos.
            if (soltados.length === 1) {
                entrada.files = soltados;
            } else {
                var uno = new DataTransfer();
                uno.items.add(soltados[0]);
                entrada.files = uno.files;
            }

            describir();
        });

        // Al volver atrás, el navegador restaura el archivo elegido pero no dispara
        // "change": sin esta llamada la zona diría "arrastre aquí" con un archivo puesto.
        describir();
    });
})();

// ---------------------------------------------------------------------------------
// Cascada Planta -> Marcador de la pantalla de carga.
//
// Filtra en el navegador y no contra el servidor porque los marcadores ya vienen todos
// en la respuesta: son un puñado de aparatos, no un catálogo que pagine.
//
// Con un solo marcador en la planta se selecciona solo. No es solo comodidad: la mayoría
// de plantas tiene uno, y obligar a elegir en un desplegable de una sola opción convierte
// una salvaguarda en un trámite que la gente aprende a despachar sin leer.
// ---------------------------------------------------------------------------------
// ---------------------------------------------------------------------------
// Marcador de origen → planta.
//
// La planta la DEFINE el aparato, así que no se pide: se muestra de solo lectura
// al elegir el marcador, y su id viaja en un hidden. Antes se elegían las dos y
// el operador podía combinar una planta con un marcador de otra — el servidor lo
// rechazaba, pero recién después de subir el archivo.
//
// El botón de cargar arranca inhabilitado. Es una ayuda para no llegar al final
// con un error evitable; el control real está en el servidor.
// ---------------------------------------------------------------------------
(function () {
    "use strict";

    var equipo = document.getElementById("equipoId");
    var plantaId = document.getElementById("plantaId");
    var plantaNombre = document.getElementById("plantaNombre");
    var boton = document.getElementById("boton-cargar");
    var aviso = document.getElementById("aviso-sin-marcador");

    if (!equipo || !plantaId || !plantaNombre) {
        return;
    }

    function sincronizar() {
        var elegido = equipo.options[equipo.selectedIndex];
        var hayEquipo = Boolean(elegido && elegido.value);

        plantaId.value = hayEquipo ? elegido.getAttribute("data-planta") : "";
        plantaNombre.value = hayEquipo ? elegido.getAttribute("data-planta-nombre") : "";

        if (boton) {
            boton.disabled = !hayEquipo;
        }

        if (aviso) {
            aviso.hidden = hayEquipo;
        }
    }

    equipo.addEventListener("change", sincronizar);

    // Al volver atrás el navegador restaura la elección anterior sin disparar
    // "change": sin esta llamada el botón quedaría bloqueado con un marcador ya
    // elegido en pantalla.
    sincronizar();
})();
