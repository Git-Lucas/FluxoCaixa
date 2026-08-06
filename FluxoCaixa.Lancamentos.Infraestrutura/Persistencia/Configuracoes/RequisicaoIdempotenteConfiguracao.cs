using FluxoCaixa.Lancamentos.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia.Configuracoes;

internal sealed class RequisicaoIdempotenteConfiguracao : IEntityTypeConfiguration<RequisicaoIdempotente>
{
    private const int _tamanhoMaximoDaChave = 64;
    private const int _tamanhoDaImpressaoSha256Hexadecimal = 64;

    public void Configure(EntityTypeBuilder<RequisicaoIdempotente> builder)
    {
        builder.ToTable("requisicao_idempotente");

        builder.Property(requisicao => requisicao.ComercianteId)
            .HasColumnName("comerciante_id")
            .HasConversion(valor => valor.Valor, valor => new ComercianteId(valor))
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(requisicao => requisicao.Chave)
            .HasColumnName("chave")
            .HasMaxLength(_tamanhoMaximoDaChave)
            .IsRequired();

        builder.HasKey(requisicao => new { requisicao.ComercianteId, requisicao.Chave });

        builder.Property(requisicao => requisicao.ImpressaoDoConteudo)
            .HasColumnName("impressao_do_conteudo")
            .HasMaxLength(_tamanhoDaImpressaoSha256Hexadecimal)
            .IsRequired();

        builder.Property(requisicao => requisicao.LancamentoId)
            .HasColumnName("lancamento_id")
            .IsRequired();

        builder.Property(requisicao => requisicao.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(requisicao => requisicao.CriadoEm)
            .HasDatabaseName("ix_requisicao_idempotente_criado_em");
    }
}
