// Inicializa los <select> con buscador una vez que el documento terminó de parsear.
//
// Estaba embebido como <script> en _Layout.cshtml; se movió a un archivo propio para que la
// Content-Security-Policy pueda declarar `script-src 'self'` SIN `'unsafe-inline'`: así un
// script inyectado en el HTML no llega a ejecutarse. El orden respecto a los scripts de cada
// vista no importa —aquellos solo escuchan "change" sobre los <select> reales y no dependen
// de que ya estén envueltos—; lo que importa es que initAll() corra una sola vez, después de
// que todo el documento terminó de parsear.
document.addEventListener("DOMContentLoaded", function () {
    SearchableSelect.initAll();
});
