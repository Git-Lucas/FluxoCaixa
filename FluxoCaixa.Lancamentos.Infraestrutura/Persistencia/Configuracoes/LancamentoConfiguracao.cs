using FluxoCaixa.Lancamentos.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FluxoCaixa.Lancamentos.Infraestrutura.Persistencia.Configuracoes;

internal sealed class LancamentoConfiguracao : IEntityTypeConfiguration<Lancamento>
{
    public void Configure(EntityTypeBuilder<Lancamento> builder)
    {
        builder.ToTable("lancamento");

        builder.HasKey(lancamento => lancamento.Id);
        builder.Property(lancamento => lancamento.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(lancamento => lancamento.ComercianteId)
            .HasColumnName("comerciante_id")
            .HasConversion(valor => valor.Valor, valor => new ComercianteId(valor))
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(lancamento => lancamento.Tipo)
            .HasColumnName("tipo")
            .HasConversion(valor => valor.ParaContrato(), valor => TipoLancamentoExtensoes.Interpretar(valor))
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(lancamento => lancamento.Valor)
            .HasColumnName("valor")
            .HasConversion(valor => valor.Valor, valor => new Dinheiro(valor))
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(lancamento => lancamento.Competencia)
            .HasColumnName("competencia")
            .HasConversion(valor => valor.Valor, valor => DataCompetencia.Reconstituir(valor))
            .HasColumnType("date")
            .IsRequired();

        builder.Property(lancamento => lancamento.Descricao)
            .HasColumnName("descricao")
            .HasConversion(valor => valor.Valor, valor => new Descricao(valor))
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(lancamento => lancamento.RecebidoEm)
            .HasColumnName("recebido_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(lancamento => lancamento.ComercianteId)
            .HasDatabaseName("ix_lancamento_comerciante_id");
    }
}
