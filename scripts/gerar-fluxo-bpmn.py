#!/usr/bin/env python3
"""Gera docs/fluxo-acordu.bpmn (BPMN 2.0 com coordenadas) e docs/fluxo-acordu.svg
(a mesma figura, para o README) a partir das listas abaixo.

Escopo: o caminho principal, da entrada da empresa ao fechamento da solicitação,
com os status da solicitação e a situação da empresa. Ficam de fora, de propósito:
cancelar a solicitação, AGUARDANDO_TERCEIRO, projetos, banco de horas e fechamento
do mês, convite da equipe, notificações e o expurgo do cadastro abandonado.

Para mudar o fluxo: edite N (nós: id, tipo, rótulo, raia, coluna, deslocamento
vertical opcional) e F (fluxos: id, origem, destino, rótulo, roteamento) e rode
  python3 scripts/gerar-fluxo-bpmn.py
Com --conferir, só confere se os dois arquivos estão em sincronia (sai 1 se não estiverem).
O arquivo gerado abre no demo.bpmn.io e no Camunda Modeler.
"""
import sys
from pathlib import Path
from xml.sax.saxutils import escape, quoteattr

DOCS = Path(__file__).resolve().parent.parent / 'docs'
SAIDAS = {'fluxo-acordu.bpmn': None, 'fluxo-acordu.svg': None}  # preenchido no fim
RAIAS = [  # nome, altura
    ("Operação Nebrys", 250),
    ("Dono da empresa", 170),
    ("Equipe (atendente/executor)", 170),
    ("Contato do cliente (portal)", 170),
    ("Sistema (jobs e e-mails)", 170),
]
CW = 175          # largura de uma coluna
X0 = 90           # início da primeira coluna (depois dos rótulos do pool e das raias)
COLUNAS = 19
SZ = {'task': (130, 80), 'start': (36, 36), 'timer': (36, 36), 'end': (36, 36), 'xor': (50, 50)}

N = [  # id, tipo, rótulo, raia, coluna[, deslocamento vertical]
    ("s_neb", "start", "Prestador quer usar o Acordu", 0, 0),
    ("t_convite", "task", "Emitir convite de empresa", 0, 1),
    ("t_cadastro", "task", "Cadastrar empresa: CPF/CNPJ, senha, aceite dos termos", 1, 2),
    ("t_cria", "task", "Criar empresa em TESTE (30 dias) e enviar confirmação", 4, 3),
    ("t_confirma", "task", "Confirmar e-mail (libera convites e e-mails)", 1, 4),
    ("tm_fim", "timer", "Fim do teste (job diário)", 4, 4),
    ("t_vencer", "task", "Vencer: VENCIDA, só leitura; Donos avisados", 4, 5),
    ("t_config", "task", "Configurar: horário útil, equipe, clientes, contratos e SLA", 1, 5),
    ("t_decide", "task", "Decidir a situação (também durante o TESTE)", 0, 6),
    ("g_sit", "xor", "Decisão", 0, 7),
    ("e_ativa", "end", "ATIVA", 0, 8, -75),
    ("e_prorroga", "end", "Prorrogada: volta a TESTE com novo fim", 0, 8),
    ("e_cancela", "end", "CANCELADA: só leitura, reativável até o expurgo", 0, 8, 75),
    ("t_convp", "task", "Convidar contato do cliente para o portal", 1, 6),
    ("t_aceita", "task", "Aceitar convite e criar acesso ao portal", 3, 7),
    ("s_tel", "start", "Cliente pede por telefone, e-mail, WhatsApp", 2, 7),
    ("t_registra", "task", "Registrar solicitação", 2, 8),
    ("t_abre", "task", "Abrir solicitação no portal", 3, 8),
    ("t_proto", "task", "Gerar protocolo, ligar o SLA e avisar (ABERTA)", 4, 9),
    ("t_triar", "task", "Triar e dar a 1ª resposta (EM_ANDAMENTO)", 2, 10),
    ("tm_rec", "timer", "Todo dia (job)", 4, 10),
    ("t_rec", "task", "Gerar tarefas das recorrências", 4, 11),
    ("t_tarefas", "task", "Criar tarefas da solicitação", 2, 11),
    ("t_exec", "task", "Executar tarefa e apontar horas (cronômetro)", 2, 12),
    ("g_cli", "xor", "Precisa de algo do cliente?", 2, 13),
    ("t_resp", "task", "Responder no portal (SLA pausado enquanto aguarda)", 3, 13),
    ("t_resolver", "task", "Resolver com a solução (RESOLVIDA)", 2, 14),
    ("t_avisa", "task", "Avisar o contato: entrega aguardando aprovação", 4, 15),
    ("g_apr", "xor", "Aprova a entrega?", 3, 16),
    ("t_fechar", "task", "Fechar (FECHADA): na aprovação ou N dias corridos depois, pelo job", 4, 17),
    ("e_fim", "end", "Solicitação fechada; horas no extrato do contrato", 4, 18),
]
F = [  # id, origem, destino, rótulo, roteamento (None = automático)
    ("f1", "s_neb", "t_convite", "", None),
    ("f2", "t_convite", "t_cadastro", "", None),
    ("f3", "t_cadastro", "t_cria", "", None),
    ("f4", "t_cria", "t_confirma", "", None),
    ("f5", "t_confirma", "t_config", "", None),
    ("f6", "t_config", "t_convp", "", None),
    ("f7", "t_convp", "t_aceita", "", None),
    ("f8", "t_aceita", "t_abre", "", None),
    ("f9", "tm_fim", "t_vencer", "", None),
    ("f10", "t_vencer", "t_decide", "", None),
    ("f11", "t_decide", "g_sit", "", None),
    ("f11a", "g_sit", "e_ativa", "Ativar", "cima"),
    ("f11b", "g_sit", "e_prorroga", "Prorrogar", None),
    ("f11c", "g_sit", "e_cancela", "Cancelar", "baixo"),
    ("f12", "s_tel", "t_registra", "", None),
    ("f13", "t_registra", "t_proto", "", None),
    ("f14", "t_abre", "t_proto", "", None),
    ("f15", "t_proto", "t_triar", "", None),
    ("f16", "t_triar", "t_tarefas", "", None),
    ("f17", "t_tarefas", "t_exec", "", None),
    ("f18", "tm_rec", "t_rec", "", None),
    ("f19", "t_rec", "t_exec", "", None),
    ("f20", "t_exec", "g_cli", "", None),
    ("f21", "g_cli", "t_resp", "Sim: AGUARDANDO_CLIENTE", None),
    ("f22", "t_resp", "t_exec", "Respondeu", "volta1"),
    ("f23", "g_cli", "t_resolver", "Não", None),
    ("f24", "t_resolver", "t_avisa", "", None),
    ("f25", "t_avisa", "g_apr", "", "entra_esq"),
    ("f26", "g_apr", "t_fechar", "Aprova", "desce"),
    ("f27", "g_apr", "t_exec", "Reprova: reabre", "volta2"),
    ("f28", "t_fechar", "e_fim", "", None),
]


def validar():
    ids = [n[0] for n in N] + [f[0] for f in F]
    repetidos = {i for i in ids if ids.count(i) > 1}
    if repetidos:
        sys.exit(f"id repetido: {sorted(repetidos)}")
    nos = {n[0] for n in N}
    for f in F:
        for ponta in (f[1], f[2]):
            if ponta not in nos:
                sys.exit(f"fluxo {f[0]} aponta para nó inexistente: {ponta}")


TOPO = [sum(h for _, h in RAIAS[:k]) for k in range(len(RAIAS))]
ALTURA = sum(h for _, h in RAIAS)
LARGURA = X0 + COLUNAS * CW + 20


def posicoes():
    pos = {}
    for n in N:
        i, t, _, raia, col = n[:5]
        dy = n[5] if len(n) > 5 else 0
        w, h = SZ[t]
        pos[i] = (X0 + col * CW + CW / 2, TOPO[raia] + RAIAS[raia][1] / 2 + dy, w, h, raia)
    return pos


def pontos(f, pos):
    _, s, t, _, modo = f
    sx, sy, sw, sh, sl = pos[s]
    tx, ty, tw, th, tl = pos[t]
    if modo == "cima":
        return [(sx, sy - sh / 2), (sx, ty), (tx - tw / 2, ty)]
    if modo == "baixo":
        return [(sx, sy + sh / 2), (sx, ty), (tx - tw / 2, ty)]
    if modo == "volta1":
        return [(sx - sw / 2, sy), (tx + 25, sy), (tx + 25, ty + th / 2)]
    if modo == "volta2":
        yb = TOPO[sl] + RAIAS[sl][1] - 14
        return [(sx, sy + sh / 2), (sx, yb), (tx - 25, yb), (tx - 25, ty + th / 2)]
    if modo == "entra_esq":
        mx = tx - tw / 2 - 40
        return [(sx + sw / 2, sy), (mx, sy), (mx, ty), (tx - tw / 2, ty)]
    if modo == "desce":
        return [(sx + sw / 2, sy), (tx, sy), (tx, ty - th / 2)]
    if abs(tx - sx) < 1:
        return [(sx, sy + sh / 2), (tx, ty - th / 2)] if ty > sy else [(sx, sy - sh / 2), (tx, ty + th / 2)]
    if sl == tl and abs(ty - sy) < 1:
        return [(sx + sw / 2, sy), (tx - tw / 2, ty)]
    mx = tx - tw / 2 - 22
    return [(sx + sw / 2, sy), (mx, sy), (mx, ty), (tx - tw / 2, ty)]


def rotulo_fluxo(f, pts, pos):
    """Canto superior esquerdo e largura do rótulo de um fluxo."""
    (ax, ay), (bx, by) = pts[-2], pts[-1]
    if f[4] in ("volta1", "volta2", "desce", None) and f[1].startswith("g_"):
        (ax, ay), (bx, by) = pts[0], pts[1]
    if f[1] == "g_sit":
        return pos[f[1]][0] + 28, pos[f[2]][1] - 18, 60
    return (ax + bx) / 2 + 4, (ay + by) / 2 - 18, 110


def rotulo_no(i, t, cx, cy, w, h):
    """Posição do rótulo externo de evento ou gateway no SVG: x, y, largura, âncora."""
    if t == 'end' and i in ("e_ativa", "e_prorroga", "e_cancela"):
        return cx + w / 2 + 8, cy, 140, 'start'
    if t == 'xor':
        return cx, cy - h / 2 - 12, 110, 'middle'
    return cx, cy + h / 2 + 12, 110, 'middle'


def gerar():
    validar()
    pos = posicoes()
    tag = {'task': 'task', 'start': 'startEvent', 'timer': 'startEvent', 'end': 'endEvent', 'xor': 'exclusiveGateway'}
    entra = {n[0]: [] for n in N}
    sai = {n[0]: [] for n in N}
    for f in F:
        sai[f[1]].append(f[0])
        entra[f[2]].append(f[0])
    o = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        '<!-- Gerado por scripts/gerar-fluxo-bpmn.py; não edite à mão. Atualize junto com a mudança de fluxo (docs/processo.md §1). -->',
        '<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="defs_acordu" targetNamespace="https://acordu.app.br/bpmn">',
        '<bpmn:collaboration id="colab"><bpmn:participant id="pool" name="Fluxo de negócio: da entrada da empresa ao fechamento da solicitação" processRef="proc"/></bpmn:collaboration>',
        '<bpmn:process id="proc" isExecutable="false"><bpmn:laneSet id="ls">',
    ]
    for k, (nome, _) in enumerate(RAIAS):
        refs = ''.join(f'<bpmn:flowNodeRef>{n[0]}</bpmn:flowNodeRef>' for n in N if n[3] == k)
        o.append(f'<bpmn:lane id="lane{k}" name={quoteattr(nome)}>{refs}</bpmn:lane>')
    o.append('</bpmn:laneSet>')
    for n in N:
        i, t, rotulo = n[:3]
        corpo = ''.join(f'<bpmn:incoming>{x}</bpmn:incoming>' for x in entra[i])
        corpo += ''.join(f'<bpmn:outgoing>{x}</bpmn:outgoing>' for x in sai[i])
        if t == 'timer':
            corpo += f'<bpmn:timerEventDefinition id="td_{i}"/>'
        o.append(f'<bpmn:{tag[t]} id="{i}" name={quoteattr(rotulo)}>{corpo}</bpmn:{tag[t]}>')
    for fid, s, t, rotulo, _ in F:
        nome = f' name={quoteattr(rotulo)}' if rotulo else ''
        o.append(f'<bpmn:sequenceFlow id="{fid}"{nome} sourceRef="{s}" targetRef="{t}"/>')
    o.append('</bpmn:process><bpmndi:BPMNDiagram id="dg"><bpmndi:BPMNPlane id="pl" bpmnElement="colab">')
    o.append(f'<bpmndi:BPMNShape id="pool_di" bpmnElement="pool" isHorizontal="true"><dc:Bounds x="0" y="0" width="{LARGURA}" height="{ALTURA}"/></bpmndi:BPMNShape>')
    for k, (_, h) in enumerate(RAIAS):
        o.append(f'<bpmndi:BPMNShape id="lane{k}_di" bpmnElement="lane{k}" isHorizontal="true"><dc:Bounds x="30" y="{TOPO[k]}" width="{LARGURA - 30}" height="{h}"/></bpmndi:BPMNShape>')
    tipo = {n[0]: n[1] for n in N}
    for i, (cx, cy, w, h, _) in pos.items():
        t = tipo[i]
        rotulo = ''
        if t == 'end' and i in ("e_ativa", "e_prorroga", "e_cancela"):
            rotulo = f'<bpmndi:BPMNLabel><dc:Bounds x="{cx + w / 2 + 6}" y="{cy - 14}" width="140" height="28"/></bpmndi:BPMNLabel>'
        elif t != 'task':
            ly = cy - h / 2 - 34 if t == 'xor' else cy + h / 2 + 4
            rotulo = f'<bpmndi:BPMNLabel><dc:Bounds x="{cx - 55}" y="{ly}" width="110" height="30"/></bpmndi:BPMNLabel>'
        marca = ' isMarkerVisible="true"' if t == 'xor' else ''
        o.append(f'<bpmndi:BPMNShape id="{i}_di" bpmnElement="{i}"{marca}><dc:Bounds x="{cx - w / 2}" y="{cy - h / 2}" width="{w}" height="{h}"/>{rotulo}</bpmndi:BPMNShape>')
    for f in F:
        pts = pontos(f, pos)
        rotulo = ''
        if f[3]:
            x, y, w = rotulo_fluxo(f, pts, pos)
            rotulo = f'<bpmndi:BPMNLabel><dc:Bounds x="{x}" y="{y}" width="{w}" height="14"/></bpmndi:BPMNLabel>'
        caminho = ''.join(f'<di:waypoint x="{x}" y="{y}"/>' for x, y in pts)
        o.append(f'<bpmndi:BPMNEdge id="{f[0]}_di" bpmnElement="{f[0]}">{caminho}{rotulo}</bpmndi:BPMNEdge>')
    o.append('</bpmndi:BPMNPlane></bpmndi:BPMNDiagram></bpmn:definitions>')
    return '\n'.join(o) + '\n'


def quebrar(texto, largura, px):
    """Quebra o rótulo em linhas que cabem em `largura` (estimativa de 0,56 em por caractere)."""
    cabe = max(4, int(largura / (px * 0.56)))
    linhas, atual = [], ''
    for palavra in texto.split():
        if atual and len(atual) + 1 + len(palavra) > cabe:
            linhas.append(atual)
            atual = palavra
        else:
            atual = f'{atual} {palavra}'.strip()
    if atual:
        linhas.append(atual)
    return linhas


def texto_svg(x, y, linhas, px, classe, ancora='middle'):
    altura = px * 1.25
    y0 = y - altura * (len(linhas) - 1) / 2
    spans = ''.join(
        f'<tspan x="{x:g}" y="{y0 + k * altura:g}">{escape(l)}</tspan>' for k, l in enumerate(linhas)
    )
    return f'<text class="{classe}" text-anchor="{ancora}" dominant-baseline="central">{spans}</text>'


def gerar_svg():
    """A mesma figura do BPMN em SVG puro, para o GitHub mostrar no README."""
    pos = posicoes()
    tipo = {n[0]: n[1] for n in N}
    rotulo_de = {n[0]: n[2] for n in N}
    o = [
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {LARGURA} {ALTURA}" width="{LARGURA}" height="{ALTURA}" role="img" aria-label="Fluxo de negócio do Acordu em BPMN">',
        '<!-- Gerado por scripts/gerar-fluxo-bpmn.py; não edite à mão. -->',
        '<style>'
        ':root{--fundo:#fbf8f1;--raia:#f4eee2;--tinta:#2a211a;--suave:#6e6153;--linha:#b9ab92;--acento:#8a6a1f}'
        '@media (prefers-color-scheme:dark){:root{--fundo:#241d17;--raia:#2b231c;--tinta:#efe6d6;--suave:#a89884;--linha:#5a4c3e;--acento:#d6b25e}}'
        '.fundo{fill:var(--fundo)}.raia{fill:var(--raia);stroke:var(--linha)}.borda{fill:none;stroke:var(--linha)}'
        '.no{fill:var(--fundo);stroke:var(--tinta);stroke-width:1.5}.fim{fill:var(--fundo);stroke:var(--tinta);stroke-width:3.5}'
        '.seta{fill:none;stroke:var(--tinta);stroke-width:1.3}.ponta{fill:var(--tinta)}.marca{stroke:var(--tinta);stroke-width:2.5}'
        '.relogio{fill:none;stroke:var(--tinta);stroke-width:1.2}'
        'text{font-family:-apple-system,"Segoe UI",Inter,Roboto,Helvetica,Arial,sans-serif;fill:var(--tinta)}'
        '.t{font-size:12px}.e{font-size:11px;fill:var(--suave)}.f{font-size:11px;fill:var(--acento)}.r{font-size:13px;font-weight:600}.p{font-size:13px;font-weight:600;fill:var(--suave)}'
        '</style>',
        '<defs><marker id="ponta" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path class="ponta" d="M0,0 L10,5 L0,10 z"/></marker></defs>',
        f'<rect class="fundo" x="0" y="0" width="{LARGURA}" height="{ALTURA}"/>',
    ]
    for k, (nome, h) in enumerate(RAIAS):
        o.append(f'<rect class="raia" x="30" y="{TOPO[k]}" width="{LARGURA - 30}" height="{h}"/>')
        o.append(f'<rect class="borda" x="30" y="{TOPO[k]}" width="30" height="{h}"/>')
        cy = TOPO[k] + h / 2
        o.append(f'<g transform="rotate(-90 45 {cy:g})">{texto_svg(45, cy, quebrar(nome, h - 10, 13), 13, "r")}</g>')
    o.append(f'<rect class="borda" x="0" y="0" width="{LARGURA}" height="{ALTURA}"/>')
    titulo = 'Fluxo de negócio: da entrada da empresa ao fechamento da solicitação'
    o.append(f'<g transform="rotate(-90 15 {ALTURA / 2:g})">{texto_svg(15, ALTURA / 2, [titulo], 13, "p")}</g>')
    for f in F:
        pts = pontos(f, pos)
        d = ' '.join(f'{x:g},{y:g}' for x, y in pts)
        o.append(f'<polyline class="seta" points="{d}" marker-end="url(#ponta)"/>')
        if f[3]:
            x, y = rotulo_fluxo(f, pts, pos)[:2]
            o.append(texto_svg(x, y + 7, [f[3]], 11, 'f', 'start'))
    for i, (cx, cy, w, h, _) in pos.items():
        t = tipo[i]
        if t == 'task':
            o.append(f'<rect class="no" x="{cx - w / 2:g}" y="{cy - h / 2:g}" width="{w}" height="{h}" rx="10"/>')
            o.append(texto_svg(cx, cy, quebrar(rotulo_de[i], w - 12, 12), 12, 't'))
            continue
        if t == 'xor':
            o.append(f'<polygon class="no" points="{cx:g},{cy - h / 2:g} {cx + w / 2:g},{cy:g} {cx:g},{cy + h / 2:g} {cx - w / 2:g},{cy:g}"/>')
            o.append(f'<path class="marca" d="M{cx - 8:g},{cy - 8:g} L{cx + 8:g},{cy + 8:g} M{cx + 8:g},{cy - 8:g} L{cx - 8:g},{cy + 8:g}"/>')
        else:
            o.append(f'<circle class="{"fim" if t == "end" else "no"}" cx="{cx:g}" cy="{cy:g}" r="{w / 2:g}"/>')
            if t == 'timer':
                o.append(f'<circle class="relogio" cx="{cx:g}" cy="{cy:g}" r="{w / 2 - 5:g}"/>')
                o.append(f'<path class="relogio" d="M{cx:g},{cy - 8:g} L{cx:g},{cy:g} L{cx + 6:g},{cy + 3:g}"/>')
        x, y, lw, ancora = rotulo_no(i, t, cx, cy, w, h)
        linhas = quebrar(rotulo_de[i], lw, 11)
        if ancora == 'start':
            o.append(texto_svg(x, y, linhas, 11, 'e', 'start'))
        elif t == 'xor':
            o.append(texto_svg(x, y - 11 * 1.25 * (len(linhas) - 1) / 2, linhas, 11, 'e'))
        else:
            o.append(texto_svg(x, y + 11 * 1.25 * (len(linhas) - 1) / 2, linhas, 11, 'e'))
    o.append('</svg>')
    return '\n'.join(o) + '\n'


if __name__ == '__main__':
    SAIDAS['fluxo-acordu.bpmn'] = gerar()
    SAIDAS['fluxo-acordu.svg'] = gerar_svg()
    for nome, conteudo in SAIDAS.items():
        arquivo = DOCS / nome
        if '--conferir' in sys.argv:
            atual = arquivo.read_text(encoding='utf-8') if arquivo.exists() else ''
            if atual != conteudo:
                sys.exit(f'docs/{nome} fora de sincronia: rode python3 scripts/gerar-fluxo-bpmn.py')
            print(f'{nome} em sincronia')
        else:
            arquivo.write_text(conteudo, encoding='utf-8')
