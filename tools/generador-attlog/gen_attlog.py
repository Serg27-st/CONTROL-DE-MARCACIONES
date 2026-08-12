# -*- coding: utf-8 -*-
"""
Genera el attlog del ZKTeco G3 para julio 2026: 50 IDs de marcador, turnos DIU/NOC/SAB/DOM1/DOM2.
Formato: ID alineado a la derecha en 9 caracteres + TAB + 'yyyy-MM-dd HH:mm:ss' + TAB + '1\t0\t1\t0' + CRLF.
Las 24 lineas originales (ids 2 y 3, 01-06 jul) se conservan byte a byte.
"""
import os
import random
import sys
from datetime import date, datetime, timedelta

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Muestra real de 24 lineas (ids 2 y 3, 01-06 jul); se conserva byte a byte en la salida.
ORIGINAL = os.path.join(RAIZ, 'tests', 'ControlMarcacion.Infrastructure.Tests',
                        'Recursos', 'AEH2200760069_attlog.txt')
# Uso: python gen_attlog.py [ruta_de_salida]
SALIDA = sys.argv[1] if len(sys.argv) > 1 else os.path.join(RAIZ, 'AEH2200760069_attlog.txt')

INICIO = date(2026, 7, 1)
FIN = date(2026, 7, 31)
ROTACION = date(2026, 7, 20)          # lunes: cambia la rotacion DIU <-> NOC
FERIADOS = {date(2026, 7, 28), date(2026, 7, 29)}

D = lambda s: date(2026, 7, s)


def rango(desde, hasta):
    d = desde
    while d <= hasta:
        yield d
        d += timedelta(days=1)


# ---------------------------------------------------------------- roster ----
# arq: DIU | NOC | ROT_DN (diurno -> nocturno el 20) | ROT_ND | DIU_SAB (sabado 07-15, solo Planta 01)
# dom: None | DOM1 (07-15) | DOM2 (07-19)
W = []


def w(mid, planta, arq, dom=None, **kw):
    W.append(dict(id=mid, planta=planta, arq=arq, dom=dom,
                  tarde=kw.get('tarde', 0.05),      # probabilidad de tardanza
                  extras=set(kw.get('extras', [])),  # dias con horas extras
                  falta=set(kw.get('falta', [])),   # dias sin marcacion
                  sola=set(kw.get('sola', [])),     # dias con una sola marca
                  dup=set(kw.get('dup', [])),       # dias con marca duplicada
                  temprano=set(kw.get('temprano', [])),  # salida antes de hora
                  desde=kw.get('desde', INICIO),    # ingreso
                  hasta=kw.get('hasta', FIN),       # cese
                  feriado=kw.get('feriado', False)))  # trabaja 28 y 29


# --- Detergentes (Consulting Advisers) --------------------------------------
w('2',  'DET', 'DIU', 'DOM2', tarde=0.0, extras=[6, 13, 20, 27], feriado=True)
w('3',  'DET', 'DIU', 'DOM2', tarde=0.0, extras=[6, 13, 20, 27], feriado=True)
w('5',  'DET', 'DIU', None,   tarde=0.02)
w('6',  'DET', 'DIU', None,   tarde=0.55, extras=[9, 23])
w('7',  'DET', 'ROT_DN', None, extras=[2, 21, 30])
w('8',  'DET', 'ROT_DN', None, tarde=0.20)
w('9',  'DET', 'ROT_ND', None, extras=[7, 22])
w('10', 'DET', 'ROT_ND', None, tarde=0.15, sola=[16])
w('11', 'DET', 'NOC', None,   tarde=0.08, feriado=True)
w('12', 'DET', 'NOC', None,   extras=[3, 10, 17, 24])
w('0014', 'DET', 'DIU', None, falta=[6, 7, 8, 9, 10, 11])            # vacaciones 06-11
w('15', 'DET', 'DIU', None,   hasta=D(15))                            # cesa el 15/07
w('16', 'DET', 'DIU', None,   desde=D(13))                            # ingresa el 13/07

# --- Planta 20 mil (Consulting Advisers) ------------------------------------
w('21', '20M', 'DIU', None,   tarde=0.10)
w('22', '20M', 'DIU', 'DOM1', tarde=0.05)
w('23', '20M', 'DIU', 'DOM1', extras=[14])
w('0024', '20M', 'DIU', None, sola=[8, 23])                           # olvida marcar salida
w('25', '20M', 'ROT_DN', None, tarde=0.12)
w('26', '20M', 'ROT_ND', None, extras=[27])
w('27', '20M', 'NOC', None,   tarde=0.10)
w('28', '20M', 'NOC', None,   falta=[15, 16])
w('29', '20M', 'DIU', None,   falta=[9, 22], tarde=0.30)
w('30', '20M', 'DIU', None,   dup=[2, 17, 30])                        # doble pasada de tarjeta
w('31', '20M', 'DIU', None,   extras=[1, 2, 6, 7, 13, 15, 20, 23, 30], feriado=True)
w('32', '20M', 'DIU', None,   falta=[20, 21, 22, 23, 24])             # descanso medico

# --- Planta 01 (CA Outsourcing) ---------------------------------------------
w('41', 'P01', 'DIU_SAB', None, tarde=0.05)
w('42', 'P01', 'DIU_SAB', None, extras=[8])
w('43', 'P01', 'DIU_SAB', None, tarde=0.60)
w('0044', 'P01', 'DIU_SAB', None, sola=[11])
w('45', 'P01', 'DIU', None,   tarde=0.05)
w('46', 'P01', 'DIU', None,   extras=[10, 24])
w('47', 'P01', 'ROT_DN', None, tarde=0.08)
w('48', 'P01', 'ROT_ND', None, extras=[13])
w('49', 'P01', 'NOC', None,   feriado=True)
w('50', 'P01', 'NOC', None,   tarde=0.12, extras=[9, 23])
w('51', 'P01', 'DIU', 'DOM2', tarde=0.05)
w('52', 'P01', 'DIU', 'DOM1')
w('53', 'P01', 'DIU', None,   feriado=True, extras=[28, 29])

# --- Sulfonacion (CA Outsourcing) -------------------------------------------
w('61', 'SUL', 'NOC', None,   feriado=True)
w('62', 'SUL', 'NOC', None,   tarde=0.10)
w('63', 'SUL', 'NOC', None,   extras=[1, 8, 15, 22, 29])
w('0064', 'SUL', 'ROT_ND', None, tarde=0.05)
w('65', 'SUL', 'ROT_DN', None, sola=[24])
w('66', 'SUL', 'DIU', None,   tarde=0.07)
w('67', 'SUL', 'DIU', None,   dup=[7])
w('68', 'SUL', 'DIU', 'DOM2', extras=[16])
w('69', 'SUL', 'DIU', None,   temprano=[3, 17, 31])
w('70', 'SUL', 'DIU', None,   feriado=True, extras=[28])
w('71', 'SUL', 'NOC', None,   falta=[6, 7])
w('72', 'SUL', 'DIU', None,   tarde=0.04)

assert len(W) == 50, len(W)
assert len({x['id'] for x in W}) == 50


# ------------------------------------------------------------------ turnos ---
def turno(worker, d):
    """Turno vigente para el trabajador en la fecha, o None si no le toca trabajar."""
    if d < worker['desde'] or d > worker['hasta']:
        return None
    if d.day in worker['falta']:
        return None
    if d in FERIADOS and not worker['feriado']:
        return None

    arq = worker['arq']
    if arq == 'ROT_DN':
        arq = 'DIU' if d < ROTACION else 'NOC'
    elif arq == 'ROT_ND':
        arq = 'NOC' if d < ROTACION else 'DIU'

    dow = d.weekday()  # 0 = lunes ... 6 = domingo
    if dow == 6:
        return worker['dom']
    if arq == 'NOC':
        return 'NOC'
    if arq == 'DIU_SAB' and dow == 5:
        return 'SAB'
    return 'DIU'


def hora(rng, h, m_desde, m_hasta):
    return timedelta(hours=h, minutes=rng.randint(m_desde, m_hasta), seconds=rng.randint(0, 59))


eventos = []  # (datetime, id)


def marca(d, delta, mid):
    ts = datetime(d.year, d.month, d.day) + delta
    if not (INICIO <= ts.date() <= FIN):
        return
    eventos.append((ts, mid))


for worker in W:
    rng = random.Random(sum(ord(c) * (i + 7) for i, c in enumerate(worker['id'])))
    mid = worker['id']

    # Salida del turno nocturno que empezo el martes 30/06: cae el 01/07 por la manana.
    previo = dict(worker, desde=date(2026, 6, 1))
    if worker['desde'] == INICIO and turno(previo, INICIO - timedelta(days=1)) == 'NOC':
        marca(INICIO, hora(rng, 7, 1, 14), mid)

    for d in rango(INICIO, FIN):
        t = turno(worker, d)
        if t is None:
            continue

        tarde = rng.random() < worker['tarde']
        if t == 'NOC':
            entrada = hora(rng, 19, 2, 26) if tarde else hora(rng, 18, 44, 59)
        else:
            entrada = hora(rng, 7, 4, 27) if tarde else hora(rng, 6, 46, 59)
        marca(d, entrada, mid)

        if d.day in worker['dup']:  # doble pasada de tarjeta
            marca(d, entrada + timedelta(seconds=rng.randint(3, 40)), mid)

        if d.day in worker['sola']:  # olvido de marcar la salida
            continue

        if t == 'NOC':
            salida = hora(rng, 7, 1, 14)
            if d.day in worker['extras']:
                salida = hora(rng, 9, 5, 55)
            marca(d + timedelta(days=1), salida, mid)
        else:
            if t in ('SAB', 'DOM1'):
                base, extra_h = 15, (16, 18)
            else:  # DIU, DOM2
                base, extra_h = 19, (20, 22)
            if d.day in worker['temprano']:
                salida = hora(rng, base - 2, 10, 55)
            elif d.day in worker['extras']:
                salida = hora(rng, rng.randint(*extra_h), 0, 45)
            else:
                salida = hora(rng, base, 1, 14)
            marca(d, salida, mid)


# ------------------------------------------------- conservar las 24 lineas ---
fijas = []
with open(ORIGINAL, 'r', newline='') as f:
    for linea in f.read().splitlines(keepends=True):
        if not linea.strip():
            continue
        campos = linea.split('\t')
        ts = datetime.strptime(campos[1], '%Y-%m-%d %H:%M:%S')
        if not linea.endswith('\r\n'):          # la muestra no termina en salto de linea
            linea = linea.rstrip('\r\n') + '\r\n'
        fijas.append((ts, campos[0].strip(), linea))

fijos_dias = {(ts.date(), mid) for ts, mid, _ in fijas}
eventos = [(ts, mid) for ts, mid in eventos if (ts.date(), mid) not in fijos_dias]

filas = [(ts, mid, '%9s\t%s\t1\t0\t1\t0\r\n' % (mid, ts.strftime('%Y-%m-%d %H:%M:%S')))
         for ts, mid in eventos]
filas.extend(fijas)
filas.sort(key=lambda x: (x[0], x[1]))

texto = ''.join(fila for _, _, fila in filas)
if texto.endswith('\r\n'):                      # se conserva la convencion de la muestra
    texto = texto[:-2]
with open(SALIDA, 'w', newline='') as f:
    f.write(texto)

print('lineas:', len(filas))
print('ids   :', len({m for _, m, _ in filas}))
print('rango :', filas[0][0], '->', filas[-1][0])
