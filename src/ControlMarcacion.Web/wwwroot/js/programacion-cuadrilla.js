(function () {
    "use strict";

    function actualizar(tipo) {
        var fila = tipo.dataset.fila;
        var turno = document.querySelector('.turno-tramo[data-fila="' + fila + '"]');
        var dias = document.querySelector('.dias-tramo[data-fila="' + fila + '"]');

        if (!turno || !dias) {
            return;
        }

        var descanso = tipo.value === "Descanso";

        turno.disabled = descanso;

        if (descanso) {
            // Un select disabled no viaja en el POST. Limpiarlo antes garantiza que MVC
            // reciba TurnoId = null y no el último turno elegido para esta fila.
            turno.value = "";
            turno.dispatchEvent(new Event("change", { bubbles: true }));
            dias.value = "1";
            dias.readOnly = true;
            return;
        }

        dias.readOnly = false;
    }

    document.querySelectorAll(".tipo-tramo").forEach(function (tipo) {
        tipo.addEventListener("change", function () {
            actualizar(tipo);
        });

        actualizar(tipo);
    });
})();
