using Acordu.Application.Abstractions;
using Acordu.Application.Common;
using Acordu.Domain.Common;
using Acordu.Domain.Entities;
using Acordu.Domain.ValueObjects;

namespace Acordu.Application.Exemplos;

/// <summary>
/// Os dados fictícios inteiros da empresa de demonstração (RF-NEB-02, F14-87 e F14-88; design do F14, D13), os mesmos do
/// manual: papéis padrão, calendário, a equipe (as contas fictícias com o papel e a capacidade do arquivo), as pessoas
/// indicadas como Dono, categorias, o que é ligado a cliente (<see cref="CargaDosDadosFicticios"/>), recorrências, o
/// cronômetro rodando e o contato com acesso ao portal. Só adiciona às unidades de trabalho, no tenant do contexto: quem
/// chama (a área da Nebrys, num escopo próprio com o tenant fixado) cria as contas, grava e faz o commit.
/// <para>
/// Nenhum efeito fora da transação: os eventos de domínio são descartados, então nada vai à outbox (nenhum e-mail, aviso
/// no sino, seguimento ou feed), como na carga dos exemplos.
/// </para>
/// </summary>
public sealed class SemeaduraDaDemonstracao
{
    /// <summary>A estimativa das tarefas das recorrências fictícias (a mesma do bot do manual).</summary>
    public static readonly Duracao EstimativaDasRecorrencias = Duracao.FromHoras(1);

    private readonly CargaDosDadosFicticios _carga;
    private readonly IRepositorio<Papel> _papeis;
    private readonly IRepositorio<Vinculo> _vinculos;
    private readonly IRepositorio<CalendarioUtil> _calendarios;
    private readonly IRepositorio<Categoria> _categorias;
    private readonly IRepositorio<RecorrenciaTarefa> _recorrencias;
    private readonly IRepositorio<Cronometro> _cronometros;
    private readonly RelogioDoTenant _relogio;

    public SemeaduraDaDemonstracao(
        CargaDosDadosFicticios carga,
        IRepositorio<Papel> papeis,
        IRepositorio<Vinculo> vinculos,
        IRepositorio<CalendarioUtil> calendarios,
        IRepositorio<Categoria> categorias,
        IRepositorio<RecorrenciaTarefa> recorrencias,
        IRepositorio<Cronometro> cronometros,
        RelogioDoTenant relogio)
    {
        ArgumentNullException.ThrowIfNull(carga);
        ArgumentNullException.ThrowIfNull(papeis);
        ArgumentNullException.ThrowIfNull(vinculos);
        ArgumentNullException.ThrowIfNull(calendarios);
        ArgumentNullException.ThrowIfNull(categorias);
        ArgumentNullException.ThrowIfNull(recorrencias);
        ArgumentNullException.ThrowIfNull(cronometros);
        ArgumentNullException.ThrowIfNull(relogio);
        _carga = carga;
        _papeis = papeis;
        _vinculos = vinculos;
        _calendarios = calendarios;
        _categorias = categorias;
        _recorrencias = recorrencias;
        _cronometros = cronometros;
        _relogio = relogio;
    }

    /// <summary>
    /// Adiciona tudo à <paramref name="tenant"/> (sem gravar) e devolve quantos clientes criou.
    /// </summary>
    /// <param name="pessoas">A conta fictícia de cada pessoa do arquivo (chave → usuário).</param>
    /// <param name="contatoDoPortal">A conta fictícia do contato com acesso ao portal; nula se o arquivo não tem portal.</param>
    /// <param name="indicadas">Contas reais que a Nebrys indicou: entram como Dono (F14-90).</param>
    public async Task<int> SemearAsync(
        DadosFicticios dados,
        Tenant tenant,
        IReadOnlyDictionary<string, Guid> pessoas,
        Guid? contatoDoPortal,
        IReadOnlyCollection<Guid> indicadas,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(pessoas);
        ArgumentNullException.ThrowIfNull(indicadas);
        var criadas = new List<EntidadeBase>();

        var papeis = Papel.Padroes(tenant.Id);
        criadas.AddRange(papeis);
        criadas.Add(CalendarioUtil.Padrao(tenant.Id));
        foreach (var pessoa in dados.Pessoas)
        {
            var vinculo = Vinculo.Criar(Guid.CreateVersion7(), pessoas[pessoa.Chave], PapelPadrao(papeis, pessoa.Papel));
            vinculo.DefinirCapacidade(Duracao.FromMinutos(pessoa.CapacidadeSemanalMinutos));
            criadas.Add(vinculo);
        }

        var dono = PapelPadrao(papeis, Papel.NomeDono);
        foreach (var usuario in indicadas.Where(u => !pessoas.Values.Contains(u)).Distinct())
            criadas.Add(Vinculo.Criar(Guid.CreateVersion7(), usuario, dono));

        var categorias = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var dado in dados.Categorias)
        {
            var categoria = Categoria.Criar(Guid.CreateVersion7(), tenant.Id, dado.Nome);
            categorias.Add(dado.Chave, categoria.Id);
            criadas.Add(categoria);
        }

        var autor = pessoas[dados.Pessoas.First(p => p.Papel == Papel.NomeDono).Chave];
        var montados = await _carga.MontarDemonstracaoAsync(dados, tenant, autor, pessoas, categorias, cancellationToken);

        var hoje = await _relogio.HojeAsync(cancellationToken);
        foreach (var dado in dados.Recorrencias)
        {
            var modelo = new ModeloTarefa(
                dado.Titulo,
                null,
                dado.Responsavel is { } responsavel ? (Guid?)pessoas[responsavel] : null,
                [],
                EstimativaDasRecorrencias,
                visivelCliente: true);
            criadas.Add(RecorrenciaTarefa.Criar(
                Guid.CreateVersion7(),
                tenant.Id,
                montados.Clientes[dado.Cliente].Id,
                montados.Contratos[dado.Cliente],
                modelo,
                new RegraRecorrencia(dado.Periodicidade, dado.DiaReferencia, dado.AntecedenciaDias, hoje),
                hoje));
        }

        if (dados.Cronometro is { } cronometro)
        {
            criadas.Add(Cronometro.Iniciar(
                Guid.CreateVersion7(), tenant.Id, pessoas[cronometro.Por], montados.Tarefas[cronometro.Tarefa], _relogio.Agora));
        }

        // O contato entra no portal como se tivesse aceitado o convite (o vínculo de cliente e a conta no contato), sem
        // convite nem e-mail.
        if (dados.Portal is { } portal && contatoDoPortal is { } contaDoContato)
        {
            var cliente = montados.Clientes[portal.Cliente];
            criadas.Add(Vinculo.Criar(
                Guid.CreateVersion7(), contaDoContato, PapelPadrao(papeis, Papel.NomeCliente), cliente.Id, portal.Escopo));
            cliente.VincularUsuarioAoContato(montados.Contatos[portal.Cliente], contaDoContato);
            cliente.LimparEventos();
        }

        foreach (var raiz in criadas)
        {
            raiz.LimparEventos();
            switch (raiz)
            {
                case Papel p:
                    _papeis.Adicionar(p);
                    break;
                case Vinculo v:
                    _vinculos.Adicionar(v);
                    break;
                case CalendarioUtil c:
                    _calendarios.Adicionar(c);
                    break;
                case Categoria c:
                    _categorias.Adicionar(c);
                    break;
                case RecorrenciaTarefa r:
                    _recorrencias.Adicionar(r);
                    break;
                case Cronometro c:
                    _cronometros.Adicionar(c);
                    break;
            }
        }

        return montados.Clientes.Count;
    }

    private static Papel PapelPadrao(IReadOnlyList<Papel> papeis, string nome) =>
        papeis.FirstOrDefault(p => p.Padrao && p.Nome == nome)
        ?? throw new InvalidOperationException($"Papel padrão {nome} ausente nos dados fictícios.");
}
