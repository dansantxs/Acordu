using Acordu.Domain.Entities;
using Acordu.Domain.Exceptions;

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
        public void DocumentoValido_GuardaSoDigitos(string documento, string esperado)
        {
            Criar(documento).Documento.Should().Be(esperado);
        }

        [Theory]
        [InlineData("529.982.247-24")]
        [InlineData("111.111.111-11")]
        [InlineData("11.222.333/0001-80")]
        [InlineData("1234567")]
        [InlineData("529.982.247-2A")]
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
}
