using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Protocols;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Składa protokół zdawczo-odbiorczy do PDF-a.
///
/// Dokument ma trafiać do teczki pracownika i do sporu, więc układ jest celowo nudny: nagłówek z danymi
/// stron, tabela mienia z numerami seryjnymi, klauzula o powierzeniu mienia, podpis i suma kontrolna.
/// Bazowa Lato pokrywa polskie znaki diakrytyczne, dlatego nie osadzamy własnego kroju.
/// </summary>
public sealed class ProtocolPdfGenerator : IProtocolPdfGenerator
{
    private static readonly Color Muted = Colors.Grey.Darken1;
    private static readonly Color Line = Colors.Grey.Lighten2;

    static ProtocolPdfGenerator()
    {
        // QuestPDF sprawdza licencję dopiero przy pierwszym renderze i wtedy rzuca wyjątkiem. Deklaracja
        // siedzi tutaj, a nie w rejestracji DI, żeby klasa działała także tam, gdzie kontener nie jest
        // budowany (testy, narzędzia). Community jest bezpłatna poniżej 1 mln USD rocznego przychodu.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(ProtocolDocument document)
    {
        var labels = document.Labels;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Lato));

                page.Header().Element(header => ComposeHeader(header, document, labels));
                page.Content().Element(content => ComposeContent(content, document, labels));
                page.Footer().Element(footer => ComposeFooter(footer, labels));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ProtocolDocument document, ProtocolLabels labels)
    {
        container.PaddingBottom(12).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(document.Kind == ProtocolKind.Return ? labels.ReturnTitle : labels.HandoverTitle)
                    .FontSize(16).Bold();
                row.ConstantItem(180).AlignRight().Text($"{labels.ProtocolNumber}: {document.ProtocolNumber}")
                    .FontSize(10).FontColor(Muted);
            });

            column.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(labels.Organization).FontSize(8).FontColor(Muted);
                    left.Item().Text(document.OrganizationName).SemiBold();
                    left.Item().PaddingTop(6).Text($"{labels.IssuedAt}: {Format(document.IssuedAt)}").FontSize(8).FontColor(Muted);
                });

                row.RelativeItem().Column(right =>
                {
                    right.Item().Text(labels.Employee).FontSize(8).FontColor(Muted);
                    right.Item().Text(document.Person.FullName).SemiBold();
                    if (!string.IsNullOrWhiteSpace(document.Person.JobTitle))
                    {
                        right.Item().Text($"{labels.JobTitle}: {document.Person.JobTitle}").FontSize(8).FontColor(Muted);
                    }

                    if (!string.IsNullOrWhiteSpace(document.Person.EmployeeNumber))
                    {
                        right.Item().Text($"{labels.EmployeeNumber}: {document.Person.EmployeeNumber}").FontSize(8).FontColor(Muted);
                    }

                    right.Item().Text($"{labels.ConfirmedAt}: {(document.ConfirmedAt is null ? labels.NotConfirmed : Format(document.ConfirmedAt.Value))}")
                        .FontSize(8).FontColor(Muted);
                });
            });

            column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Line);
        });
    }

    private static void ComposeContent(IContainer container, ProtocolDocument document, ProtocolLabels labels)
    {
        container.Column(column =>
        {
            column.Spacing(14);

            if (document.Lines.Count == 0)
            {
                column.Item().Text(labels.NoItems).FontColor(Muted);
            }
            else
            {
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), labels.Item);
                        HeaderCell(header.Cell(), labels.AssetTag);
                        HeaderCell(header.Cell(), labels.SerialNumber);
                        HeaderCell(header.Cell(), labels.Condition);
                        HeaderCell(header.Cell(), labels.Value);
                    });

                    foreach (var line in document.Lines)
                    {
                        BodyCell(table.Cell(), line.Name, semiBold: true);
                        BodyCell(table.Cell(), line.AssetTag);
                        BodyCell(table.Cell(), line.SerialNumber);
                        BodyCell(table.Cell(), string.IsNullOrWhiteSpace(line.Status) ? line.Condition : $"{line.Status}{(string.IsNullOrWhiteSpace(line.Condition) ? "" : $" - {line.Condition}")}");
                        BodyCell(table.Cell(), FormatValue(line));
                    }
                });
            }

            if (document.Procedures.Count > 0)
            {
                column.Item().Column(procedures =>
                {
                    procedures.Item().Text(labels.Procedures).FontSize(8).FontColor(Muted);
                    foreach (var procedure in document.Procedures)
                    {
                        procedures.Item().Text($"- {procedure}");
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(document.Notes))
            {
                column.Item().Column(notes =>
                {
                    notes.Item().Text(labels.Notes).FontSize(8).FontColor(Muted);
                    notes.Item().Text(document.Notes);
                });
            }

            column.Item().PaddingTop(4).Text(labels.LiabilityClause).FontSize(8).LineHeight(1.4f);

            column.Item().PaddingTop(10).Column(hash =>
            {
                if (string.IsNullOrWhiteSpace(document.ConfirmationHash)) return;
                hash.Item().Text(labels.IntegrityHash).FontSize(8).FontColor(Muted);
                hash.Item().PaddingTop(4).Text(document.ConfirmationHash.ToLowerInvariant()).FontSize(7).FontColor(Muted);
            });
        });
    }

    private static void ComposeFooter(IContainer container, ProtocolLabels labels)
    {
        container.PaddingTop(10).Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Line);
            column.Item().PaddingTop(6).Text(labels.LegalNote).FontSize(7).FontColor(Muted).LineHeight(1.3f);
            column.Item().PaddingTop(4).AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(7).FontColor(Muted));
                text.Span($"{labels.Page} ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private static void HeaderCell(IContainer cell, string text) =>
        cell.BorderBottom(1).BorderColor(Line).PaddingVertical(5).Text(text).FontSize(8).SemiBold().FontColor(Muted);

    private static void BodyCell(IContainer cell, string? text, bool semiBold = false)
    {
        var span = cell.BorderBottom(1).BorderColor(Line).PaddingVertical(5).Text(string.IsNullOrWhiteSpace(text) ? "-" : text);
        if (semiBold) span.SemiBold();
    }

    private static string FormatValue(ProtocolLine line) =>
        line.Value is null ? "-" : $"{line.Value.Value:N2} {line.Currency ?? string.Empty}".Trim();

    private static string Format(DateTimeOffset value) => value.ToString("yyyy-MM-dd HH:mm");
}
