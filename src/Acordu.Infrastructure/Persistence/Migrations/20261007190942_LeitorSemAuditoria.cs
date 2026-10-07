using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acordu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeitorSemAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Só dados: o Leitor padrão dos tenants já registrados perde auditoria.ver (segurança §3.2).
            migrationBuilder.Sql("UPDATE papeis SET permissoes = array_remove(permissoes, 'auditoria.ver') WHERE padrao AND nome = 'Leitor';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE papeis SET permissoes = array_append(permissoes, 'auditoria.ver') WHERE padrao AND nome = 'Leitor' AND NOT ('auditoria.ver' = ANY(permissoes));");
        }
    }
}
