using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FluxoCaixa.Consolidado.Api.Persistencia.Configuracoes;

internal sealed class ConsolidadoDiarioConfiguracao : IEntityTypeConfiguration<ConsolidadoDiario>
{
    public void Configure(EntityTypeBuilder<ConsolidadoDiario> builder)
    {
        builder.ToTable("consolidado_diario");

        builder.HasKey(consolidado => new { consolidado.ComercianteId, consolidado.Competencia });

        builder.Property(consolidado => consolidado.ComercianteId)
            .HasColumnName("comerciante_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(consolidado => consolidado.Competencia)
            .HasColumnName("competencia")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(consolidado => consolidado.TotalCredito)
            .HasColumnName("total_credito")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(consolidado => consolidado.TotalDebito)
            .HasColumnName("total_debito")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(consolidado => consolidado.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
