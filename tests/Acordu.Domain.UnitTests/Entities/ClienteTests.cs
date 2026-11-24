using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;
using Acordu.Domain.ValueObjects;

namespace Acordu.Domain.UnitTests.Entities;

public sealed class ClienteTests
{
    private static Cliente Criar(string? documento = null) =>
        Cliente.Criar(Guid.NewGuid(), Calendarios.Tenant, "Padaria Bom Grão", documento, "Atende só pela manhã");

    public sealed class CriarCliente
    {
        [Fact]
        public void Valido_NasceAtivoSemContatos()
        {
            var cliente = Criar();

            cliente.Nome.Should().Be("Padaria Bom Grão");
            cliente.Observacoes.Should().Be("Atende só pela manhã");
            cliente.Ativo.Should().BeTrue();
            cliente.Contatos.Should().BeEmpty();
        }

        [Fact]
        public void SemNome_LancaExcecao()
        {
            var acao = () => Cliente.Criar(Guid.NewGuid(), Calendarios.Tenant, " ", null, null);

            acao.Should().Throw<ArgumentException>().WithParameterName("nome");
        }

        [Theory]
        [InlineData("529.982.247-25", "52998224725")]
        [InlineData("52998224725", "52998224725")]
        [InlineData("11.222.333/0001-81", "11222333000181")]
        // CNPJ alfanumérico: exemplo da Receita; minúsculas viram maiúsculas.
        [InlineData("12.ABC.345/01DE-35", "12ABC34501DE35")]
        [InlineData("12.abc.345/01de-35", "12ABC34501DE35")]
        [InlineData("12ABC34501DE35", "12ABC34501DE35")]
        public void DocumentoValido_GuardaSemPontuacaoEmMaiusculas(string documento, string esperado)
        {
            Criar(documento).Documento.Should().Be(esperado);
        }

        [Theory]
        [InlineData("529.982.247-24")]
        [InlineData("111.111.111-11")]
        [InlineData("11.222.333/0001-80")]
        [InlineData("1234567")]
        [InlineData("529.982.247-2A")]
        [InlineData("12.ABC.345/01DE-36")]
        [InlineData("12.ABC.345/01DE-3A")]
        [InlineData("ABC.DEF.GHI-JK")]
        [InlineData("12.ABC.345/01DE-35!")]
        public void DocumentoInvalido_LancaExcecao(string documentoInformado)
        {
            var acao = () => Criar(documentoInformado);

            acao.Should().Throw<ArgumentException>().WithParameterName("documento");
        }

        [Fact]
        public void DocumentoEmBranco_FicaNulo()
        {
            Criar("  ").Documento.Should().BeNull();
        }
    }

    public sealed class DadosDeExemplo
    {
        [Fact]
        public void Criar_SemMarca_NaoEhExemplo()
        {
            Criar().Exemplo.Should().BeFalse();
        }

        [Fact]
        public void MarcarComoExemplo_ClienteNovo_FicaMarcadoEAtivo()
        {
            var cliente = Criar();

            cliente.MarcarComoExemplo();

            cliente.Exemplo.Should().BeTrue();
            cliente.Ativo.Should().BeTrue();
        }
    }

    public sealed class EditarEInativar
    {
        [Fact]
        public void Editar_AlteraDados()
        {
            var cliente = Criar();

            cliente.Editar("Padaria Bom Grão Ltda", "11.222.333/0001-81", null);

            cliente.Nome.Should().Be("Padaria Bom Grão Ltda");
            cliente.Documento.Should().Be("11222333000181");
            cliente.Observacoes.Should().BeNull();
        }

        [Fact]
        public void Editar_DocumentoInvalido_NaoAlteraNada()
        {
            var cliente = Criar();

            var acao = () => cliente.Editar("Outro nome", "123", null);

            acao.Should().Throw<ArgumentException>();
            cliente.Nome.Should().Be("Padaria Bom Grão");
            cliente.Observacoes.Should().Be("Atende só pela manhã");
        }

        [Fact]
        public void InativarEReativar()
        {
            var cliente = Criar();

            cliente.Inativar();
            cliente.Ativo.Should().BeFalse();

            cliente.Reativar();
            cliente.Ativo.Should().BeTrue();
        }
    }

    public sealed class Contatos
    {
        [Fact]
        public void Adicionar_NormalizaEmailENasceAtivo()
        {
            var cliente = Criar();

            var contato = cliente.AdicionarContato("Marina", " Marina@BomGrao.com.br ", "(14) 99999-0000", "Sócia");

            cliente.Contatos.Should().ContainSingle();
            contato.Email.Should().Be("marina@bomgrao.com.br");
            contato.Telefone.Should().Be("(14) 99999-0000");
            contato.Cargo.Should().Be("Sócia");
            contato.Ativo.Should().BeTrue();
            contato.UsuarioId.Should().BeNull();
            contato.Id.Should().NotBe(Guid.Empty);
        }

        [Theory]
        [InlineData("marina")]
        [InlineData("marina@")]
        [InlineData("@bomgrao.com")]
        [InlineData("marina@bomgrao")]
        [InlineData("ma rina@bomgrao.com")]
        [InlineData("marina@@bomgrao.com")]
        [InlineData("marina@bomgrao.com.")]
        public void EmailInvalido_LancaExcecao(string emailInformado)
        {
            var acao = () => Criar().AdicionarContato("Marina", emailInformado, null, null);

            acao.Should().Throw<ArgumentException>().WithParameterName("email");
        }

        [Fact]
        public void EmailDuplicadoEntreAtivos_LancaExcecao()
        {
            var cliente = Criar();
            cliente.AdicionarContato("Marina", "marina@bomgrao.com.br", null, null);

            var acao = () => cliente.AdicionarContato("Marina 2", "MARINA@bomgrao.com.br", null, null);

            acao.Should().Throw<EmailDuplicadoException>();
            cliente.Contatos.Should().ContainSingle();
        }

        [Fact]
        public void EmailDeContatoInativo_PodeSerReutilizado()
        {
            var cliente = Criar();
            var antigo = cliente.AdicionarContato("Marina", "marina@bomgrao.com.br", null, null);
            cliente.InativarContato(antigo.Id);

            var acao = () => cliente.AdicionarContato("Marina (nova)", "marina@bomgrao.com.br", null, null);

            acao.Should().NotThrow();
        }

        [Fact]
        public void Atualizar_ProprioEmail_NaoContaComoDuplicado()
        {
            var cliente = Criar();
            var contato = cliente.AdicionarContato("Marina", "marina@bomgrao.com.br", null, null);

            cliente.AtualizarContato(contato.Id, "Marina Souza", "marina@bomgrao.com.br", null, "Gerente");

            contato.Nome.Should().Be("Marina Souza");
            contato.Cargo.Should().Be("Gerente");
        }

        [Fact]
        public void Atualizar_ParaEmailDeOutroAtivo_LancaExcecao()
        {
            var cliente = Criar();
            cliente.AdicionarContato("Marina", "marina@bomgrao.com.br", null, null);
            var jose = cliente.AdicionarContato("José", "jose@bomgrao.com.br", null, null);

            var acao = () => cliente.AtualizarContato(jose.Id, "José", "marina@bomgrao.com.br", null, null);

            acao.Should().Throw<EmailDuplicadoException>();
            jose.Email.Should().Be("jose@bomgrao.com.br");
        }

        [Fact]
        public void ContatoInexistente_LancaExcecao()
        {
            var acao = () => Criar().InativarContato(Guid.NewGuid());

            acao.Should().Throw<ArgumentException>().WithParameterName("contatoId");
        }

        [Fact]
        public void VincularUsuario_ContatoAtivo_Vincula()
        {
            var cliente = Criar();
            var contato = cliente.AdicionarContato("Marina", "marina@bomgrao.com.br", null, null);
            var usuario = Guid.NewGuid();

            cliente.VincularUsuarioAoContato(contato.Id, usuario);
            cliente.VincularUsuarioAoContato(contato.Id, usuario);

            contato.UsuarioId.Should().Be(usuario);
        }

        [Fact]
        public void VincularUsuario_ContatoInativo_LancaExcecao()
        {
            var cliente = Criar();
            var contato = cliente.AdicionarContato("Marina", "marina@bomgrao.com.br", null, null);
            cliente.InativarContato(contato.Id);

            var acao = () => cliente.VincularUsuarioAoContato(contato.Id, Guid.NewGuid());

            acao.Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void VincularUsuario_JaVinculadoAOutro_LancaExcecao()
        {
            var cliente = Criar();
            var contato = cliente.AdicionarContato("Marina", "marina@bomgrao.com.br", null, null);
            cliente.VincularUsuarioAoContato(contato.Id, Guid.NewGuid());

            var acao = () => cliente.VincularUsuarioAoContato(contato.Id, Guid.NewGuid());

            acao.Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void DesvincularUsuario_LimpaOUsuario_EPermiteVincularOutro()
        {
            var cliente = Criar();
            var contato = cliente.AdicionarContato("Marina", "marina@bomgrao.com.br", null, null);
            cliente.VincularUsuarioAoContato(contato.Id, Guid.NewGuid());
            var outro = Guid.NewGuid();

            cliente.DesvincularUsuarioDoContato(contato.Id);
            contato.UsuarioId.Should().BeNull();
            cliente.VincularUsuarioAoContato(contato.Id, outro);

            contato.UsuarioId.Should().Be(outro);
        }
    }

    public sealed class CadastroAmpliado
    {
        private static Endereco Endereco(string cep = "19700-000", string uf = "sp") =>
            new(cep, "Rua das Flores", "120", null, "Centro", "Bastos", uf);

        [Fact]
        public void DefinirCadastro_NormalizaEApagaComNulo()
        {
            var cliente = Criar();

            cliente.DefinirCadastro(" Bom Grão Panificadora Ltda ", "123.456.789.110", "Contato@BomGrao.com", " (18) 3333-0000 ", Endereco());

            cliente.RazaoSocial.Should().Be("Bom Grão Panificadora Ltda");
            cliente.InscricaoEstadual.Should().Be("123.456.789.110");
            cliente.Email.Should().Be("contato@bomgrao.com");
            cliente.Telefone.Should().Be("(18) 3333-0000");
            cliente.Endereco!.Cep.Should().Be("19700000");
            cliente.Endereco.Uf.Should().Be("SP");

            cliente.DefinirCadastro(null, " ", null, null, null);

            cliente.RazaoSocial.Should().BeNull();
            cliente.InscricaoEstadual.Should().BeNull();
            cliente.Email.Should().BeNull();
            cliente.Endereco.Should().BeNull();
        }

        [Theory]
        [InlineData("isento", "ISENTO")]
        [InlineData("110.042.490.114", "110.042.490.114")]
        public void InscricaoEstadual_AceitaNumeroOuIsento(string informada, string esperada)
        {
            var cliente = Criar();

            cliente.DefinirCadastro(null, informada, null, null, null);

            cliente.InscricaoEstadual.Should().Be(esperada);
        }

        [Theory]
        [InlineData("isenta")]
        [InlineData("---")]
        public void InscricaoEstadual_Invalida_LancaNoCampo(string informada)
        {
            var acao = () => Criar().DefinirCadastro(null, informada, null, null, null);

            acao.Should().Throw<ArgumentException>().WithParameterName("inscricaoEstadual");
        }

        [Fact]
        public void EmailInvalido_LancaNoCampo()
        {
            var acao = () => Criar().DefinirCadastro(null, null, "sem-arroba", null, null);

            acao.Should().Throw<ArgumentException>().WithParameterName("email");
        }

        [Theory]
        [InlineData("1970-000", "SP", "cep")]
        [InlineData("19700-00A", "SP", "cep")]
        [InlineData("19700-000", "XX", "uf")]
        public void Endereco_Invalido_LancaNoCampo(string cep, string uf, string campo)
        {
            var acao = () => Endereco(cep, uf);

            acao.Should().Throw<ArgumentException>().WithParameterName(campo);
        }

        [Fact]
        public void Etiquetas_AparaDeduplicaEMantemAPrimeiraGrafia()
        {
            var cliente = Criar();

            cliente.DefinirEtiquetas(["  Varejo ", "varejo", "Condomínio   residencial", "", "VAREJO"]);

            cliente.Etiquetas.Should().Equal("Varejo", "Condomínio residencial");
        }

        [Fact]
        public void Etiquetas_AcimaDoLimite_LancaNoCampo()
        {
            var acao = () => Criar().DefinirEtiquetas(Enumerable.Range(1, Cliente.MaximoEtiquetas + 1).Select(i => $"e{i}"));

            acao.Should().Throw<ArgumentException>().WithParameterName("etiquetas");
        }

        [Fact]
        public void Etiqueta_Longa_LancaNoCampo()
        {
            var acao = () => Criar().DefinirEtiquetas([new string('a', 31)]);

            acao.Should().Throw<ArgumentException>().WithParameterName("etiquetas");
        }

        [Fact]
        public void Responsavel_DefineEApaga()
        {
            var cliente = Criar();
            var pessoa = Guid.NewGuid();

            cliente.DefinirResponsavel(pessoa);
            cliente.ResponsavelId.Should().Be(pessoa);

            cliente.DefinirResponsavel(null);
            cliente.ResponsavelId.Should().BeNull();
        }
    }

    public sealed class Reativacao
    {
        [Fact]
        public void Cliente_VoltaAtivo()
        {
            var cliente = Criar();
            cliente.Inativar();

            cliente.Reativar();

            cliente.Ativo.Should().BeTrue();
        }

        [Fact]
        public void Contato_VoltaAtivo()
        {
            var cliente = Criar();
            var contato = cliente.AdicionarContato("Marta", "marta@bomgrao.com", null, null);
            cliente.InativarContato(contato.Id);

            cliente.ReativarContato(contato.Id);

            contato.Ativo.Should().BeTrue();
        }

        [Fact]
        public void Contato_ComClienteInativo_Lanca()
        {
            var cliente = Criar();
            var contato = cliente.AdicionarContato("Marta", "marta@bomgrao.com", null, null);
            cliente.InativarContato(contato.Id);
            cliente.Inativar();

            var acao = () => cliente.ReativarContato(contato.Id);

            acao.Should().Throw<TransicaoInvalidaException>();
        }

        [Fact]
        public void Contato_ComEmailJaUsadoPorOutroAtivo_Lanca()
        {
            var cliente = Criar();
            var antigo = cliente.AdicionarContato("Marta", "marta@bomgrao.com", null, null);
            cliente.InativarContato(antigo.Id);
            cliente.AdicionarContato("Marta Souza", "marta@bomgrao.com", null, null);

            var acao = () => cliente.ReativarContato(antigo.Id);

            acao.Should().Throw<EmailDuplicadoException>();
        }
    }
}
