#!/usr/bin/env python3
"""Gera docs/fluxo-acordu.bpmn (BPMN 2.0 com coordenadas) a partir das listas abaixo.

Para mudar o fluxo: edite N (nós: id, tipo, rótulo, raia, coluna) e F (fluxos:
id, origem, destino, rótulo, roteamento especial) e rode
  python3 scripts/gerar-fluxo-bpmn.py
O arquivo gerado abre no demo.bpmn.io e no Camunda Modeler.
"""
from pathlib import Path
from xml.sax.saxutils import escape as e
LANES=["Operação Nebrys","Dono da empresa","Equipe (atendente/executor)","Contato do cliente (portal)","Acordu (sistema, jobs e e-mails)"]
LH=170; CW=175; X0=60+30; PW=X0
SZ={'task':(130,80),'start':(36,36),'timer':(36,36),'end':(36,36),'xor':(50,50)}
N=[ # id,type,label,lane,col
("s_neb","start","Prestador quer usar o Acordu",0,0),
("t_convite","task","Emitir convite de empresa (ou lista de espera)",0,1),
("t_cadastro","task","Cadastrar empresa: CPF/CNPJ, senha, aceite dos termos",1,2),
("t_cria","task","Criar empresa em TESTE (30 dias) e enviar confirmação",4,3),
("t_confirma","task","Confirmar e-mail (libera convites e e-mails)",1,4),
("tm_fim","timer","Fim do teste (job diário)",4,4),
("t_vencer","task","Vencer: empresa fica só leitura e Donos são avisados",4,5),
("t_config","task","Configurar: horário útil, equipe, clientes, contratos e SLA",1,5),
("t_decide","task","Ativar, prorrogar o teste ou cancelar",0,6),
("e_sit","end","Situação definida",0,7),
("t_convp","task","Convidar contato do cliente para o portal",1,6),
("t_aceita","task","Aceitar convite e criar acesso ao portal",3,7),
("s_tel","start","Cliente pede por telefone, e-mail, WhatsApp",2,7),
("t_registra","task","Registrar solicitação",2,8),
("t_abre","task","Abrir solicitação no portal",3,8),
("t_proto","task","Gerar protocolo, ligar relógio do SLA e avisar por e-mail",4,9),
("t_triar","task","Triar e dar a 1ª resposta (Em andamento)",2,10),
("tm_rec","timer","Todo dia (job)",4,10),
("t_rec","task","Gerar tarefas das recorrências",4,11),
("t_tarefas","task","Criar tarefas da solicitação",2,11),
("t_exec","task","Executar tarefa e apontar horas (cronômetro)",2,12),
("g_cli","xor","Precisa de algo do cliente?",2,13),
("t_resp","task","Responder no portal (SLA pausado enquanto aguarda)",3,13),
("t_resolver","task","Resolver com a solução",2,14),
("t_avisa","task","Avisar o contato: entrega aguardando aprovação",4,15),
("g_apr","xor","Aprova a entrega?",3,16),
("t_fechar","task","Fechar solicitação (na aprovação ou após N dias)",4,17),
("e_fim","end","Solicitação fechada; horas no extrato do contrato",4,18),
]
F=[ # id,src,tgt,label,mode
("f1","s_neb","t_convite","",None),("f2","t_convite","t_cadastro","",None),
("f3","t_cadastro","t_cria","",None),("f4","t_cria","t_confirma","",None),
("f5","t_confirma","t_config","",None),("f6","t_config","t_convp","",None),
("f7","t_convp","t_aceita","",None),("f8","t_aceita","t_abre","",None),
("f9","tm_fim","t_vencer","",None),("f10","t_vencer","t_decide","",None),("f11","t_decide","e_sit","",None),
("f12","s_tel","t_registra","",None),("f13","t_registra","t_proto","",None),("f14","t_abre","t_proto","",None),
("f15","t_proto","t_triar","",None),("f16","t_triar","t_tarefas","",None),("f17","t_tarefas","t_exec","",None),
("f18","tm_rec","t_rec","",None),("f19","t_rec","t_exec","",None),
("f20","t_exec","g_cli","",None),("f21","g_cli","t_resp","Sim",None),("f22","t_resp","t_exec","","back1"),
("f23","g_cli","t_resolver","Não",None),("f24","t_resolver","t_avisa","",None),("f25","t_avisa","g_apr","",None),
("f26","g_apr","t_fechar","Aprova",None),("f27","g_apr","t_exec","Reprova: reabre","back2"),("f28","t_fechar","e_fim","",None),
]
pos={}
for i,t,l,ln,c in N:
    w,h=SZ[t]; cx=X0+c*CW+CW/2; cy=ln*LH+LH/2
    pos[i]=(cx,cy,w,h,ln,t)
TW=X0+19*CW+20
def wp(f):
    _,s,t,_,m=f; sx,sy,sw,sh,sl,_=pos[s]; tx,ty,tw,th,tl,_=pos[t]
    if m=="back1": return [(sx-sw/2,sy),(tx+25,sy),(tx+25,ty+th/2)]
    if m=="back2": yb=sl*LH+LH-14; return [(sx,sy+sh/2),(sx,yb),(tx-25,yb),(tx-25,ty+th/2)]
    if abs(tx-sx)<1: return [(sx,sy+sh/2),(tx,ty-th/2)] if ty>sy else [(sx,sy-sh/2),(tx,ty+th/2)]
    if sl==tl: return [(sx+sw/2,sy),(tx-tw/2,ty)]
    mx=tx-tw/2-22 if t!="g_apr" else tx
    if t=="g_apr": return [(sx+sw/2,sy),(tx-tw/2-40,sy),(tx-tw/2-40,ty),(tx-tw/2,ty)]
    if s=="g_apr": return [(sx+sw/2,sy),(tx,sy),(tx,ty-th/2)]
    return [(sx+sw/2,sy),(mx,sy),(mx,ty),(tx-tw/2,ty)]
tag={'task':'task','start':'startEvent','timer':'startEvent','end':'endEvent','xor':'exclusiveGateway'}
inc={i:[] for i,*_ in N}; out={i:[] for i,*_ in N}
for f in F: out[f[1]].append(f[0]); inc[f[2]].append(f[0])
o=['<?xml version="1.0" encoding="UTF-8"?>','<!-- Gerado por scripts/gerar-fluxo-bpmn.py; não edite à mão. Atualize junto com a mudança de fluxo (docs/processo.md §1). -->',
'<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="defs_acordu" targetNamespace="https://acordu.app.br/bpmn">',
'<bpmn:collaboration id="colab"><bpmn:participant id="pool" name="Acordu: da entrada da empresa ao fechamento da solicitação" processRef="proc"/></bpmn:collaboration>',
'<bpmn:process id="proc" isExecutable="false"><bpmn:laneSet id="ls">']
for k,ln in enumerate(LANES):
    o.append(f'<bpmn:lane id="lane{k}" name="{e(ln)}">'+''.join(f'<bpmn:flowNodeRef>{i}</bpmn:flowNodeRef>' for i,_,_,l,_ in N if l==k)+'</bpmn:lane>')
o.append('</bpmn:laneSet>')
for i,t,l,ln,c in N:
    body=''.join(f'<bpmn:incoming>{x}</bpmn:incoming>' for x in inc[i])+''.join(f'<bpmn:outgoing>{x}</bpmn:outgoing>' for x in out[i])
    if t=='timer': body+=f'<bpmn:timerEventDefinition id="td_{i}"/>'
    o.append(f'<bpmn:{tag[t]} id="{i}" name="{e(l)}">{body}</bpmn:{tag[t]}>')
for fid,s,t,l,_ in F:
    o.append(f'<bpmn:sequenceFlow id="{fid}" name="{e(l)}" sourceRef="{s}" targetRef="{t}"/>')
o.append('</bpmn:process><bpmndi:BPMNDiagram id="dg"><bpmndi:BPMNPlane id="pl" bpmnElement="colab">')
o.append(f'<bpmndi:BPMNShape id="pool_di" bpmnElement="pool" isHorizontal="true"><dc:Bounds x="0" y="0" width="{TW}" height="{LH*len(LANES)}"/></bpmndi:BPMNShape>')
for k in range(len(LANES)):
    o.append(f'<bpmndi:BPMNShape id="lane{k}_di" bpmnElement="lane{k}" isHorizontal="true"><dc:Bounds x="30" y="{k*LH}" width="{TW-30}" height="{LH}"/></bpmndi:BPMNShape>')
for i,(cx,cy,w,h,ln,t) in pos.items():
    lab=''
    ly=cy-h/2-34 if t=='xor' else cy+h/2+4
    if t!='task': lab=f'<bpmndi:BPMNLabel><dc:Bounds x="{cx-55}" y="{ly}" width="110" height="30"/></bpmndi:BPMNLabel>'
    mk=' isMarkerVisible="true"' if t=='xor' else ''
    o.append(f'<bpmndi:BPMNShape id="{i}_di" bpmnElement="{i}"{mk}><dc:Bounds x="{cx-w/2}" y="{cy-h/2}" width="{w}" height="{h}"/>{lab}</bpmndi:BPMNShape>')
for f in F:
    pts=wp(f); lab=''
    if f[3]:
        (ax,ay),(bx,by)=pts[0],pts[1]
        lab=f'<bpmndi:BPMNLabel><dc:Bounds x="{(ax+bx)/2+4}" y="{(ay+by)/2-18}" width="90" height="14"/></bpmndi:BPMNLabel>'
    o.append(f'<bpmndi:BPMNEdge id="{f[0]}_di" bpmnElement="{f[0]}">'+''.join(f'<di:waypoint x="{x}" y="{y}"/>' for x,y in pts)+lab+'</bpmndi:BPMNEdge>')
o.append('</bpmndi:BPMNPlane></bpmndi:BPMNDiagram></bpmn:definitions>')
Path(__file__).resolve().parent.parent.joinpath('docs','fluxo-acordu.bpmn').write_text('\n'.join(o)+'\n',encoding='utf-8')
