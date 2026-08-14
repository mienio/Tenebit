using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Tenebit.Application.Abstractions;

namespace Tenebit.Infrastructure.Services;

public sealed class PdfProtocolGenerator : IPdfProtocolGenerator
{
    public byte[] GenerateHandoverProtocol(ProtocolPdfModel model)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(header =>
                        {
                            header.Item().Text(model.OrganizationName).FontSize(16).Bold();
                            header.Item().Text(model.OrganizationCountry).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(180).AlignRight().Column(header =>
                        {
                            header.Item().Text("Protokół wydania / zwrotu sprzętu").FontSize(11).Bold();
                            header.Item().Text($"Nr protokołu: {model.ProtocolNumber}").FontColor(Colors.Grey.Darken1);
                        });
                    });
                    column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Column(person =>
                    {
                        person.Item().Text("Pracownik").Bold();
                        person.Item().Text(model.PersonFullName);
                        if (!string.IsNullOrWhiteSpace(model.PersonJobTitle)) person.Item().Text(model.PersonJobTitle);
                        if (!string.IsNullOrWhiteSpace(model.TeamName)) person.Item().Text($"Zespół: {model.TeamName}");
                    });

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Data wydania: {model.IssuedAt:yyyy-MM-dd}");
                        if (model.DueDate.HasValue) row.RelativeItem().Text($"Termin zwrotu: {model.DueDate:yyyy-MM-dd}");
                        if (model.AcceptedAt.HasValue) row.RelativeItem().Text($"Data akceptacji: {model.AcceptedAt:yyyy-MM-dd}");
                        if (model.ReturnedAt.HasValue) row.RelativeItem().Text($"Data zwrotu: {model.ReturnedAt:yyyy-MM-dd}");
                    });

                    column.Item().Column(assetsSection =>
                    {
                        assetsSection.Item().Text("Sprzęt").Bold();
                        assetsSection.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Nazwa");
                                header.Cell().Element(HeaderCell).Text("Tag");
                                header.Cell().Element(HeaderCell).Text("Nr seryjny");
                                header.Cell().Element(HeaderCell).Text("Stan wydania");
                                header.Cell().Element(HeaderCell).Text("Stan zwrotu");

                                static IContainer HeaderCell(IContainer container) => container
                                    .DefaultTextStyle(style => style.Bold())
                                    .PaddingBottom(4)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Darken1);
                            });

                            foreach (var asset in model.Assets)
                            {
                                table.Cell().Element(BodyCell).Text(asset.Name);
                                table.Cell().Element(BodyCell).Text(asset.AssetTag);
                                table.Cell().Element(BodyCell).Text(asset.SerialNumber ?? "—");
                                table.Cell().Element(BodyCell).Text(asset.IssueCondition);
                                table.Cell().Element(BodyCell).Text(asset.ReturnCondition ?? "—");
                            }

                            static IContainer BodyCell(IContainer container) => container
                                .PaddingVertical(4)
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2);
                        });
                    });

                    if (model.ProcedureTitlesRequiringAcceptance.Count > 0)
                    {
                        column.Item().Column(procedures =>
                        {
                            procedures.Item().Text("Procedury i regulaminy do zapoznania").Bold();
                            foreach (var title in model.ProcedureTitlesRequiringAcceptance)
                            {
                                procedures.Item().Text($"• {title}");
                            }
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(model.Notes))
                    {
                        column.Item().Column(notes =>
                        {
                            notes.Item().Text("Uwagi").Bold();
                            notes.Item().Text(model.Notes);
                        });
                    }

                    column.Item().PaddingTop(10).Text(
                        "Oświadczam, że odebrałem/-am wymieniony powyżej sprzęt w stanie wskazanym w protokole oraz zapoznałem/-am się z wymienionymi procedurami i regulaminami dotyczącymi jego użytkowania.");

                    column.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(signature =>
                        {
                            signature.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                            signature.Item().PaddingTop(2).Text("Podpis pracownika").FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(30);
                        row.RelativeItem().Column(signature =>
                        {
                            signature.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                            signature.Item().PaddingTop(2).Text("Data").FontColor(Colors.Grey.Darken1);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Wygenerowano automatycznie przez Tenebit — ").FontColor(Colors.Grey.Darken1);
                    text.Span(model.ProtocolNumber).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateOffboardingProtocol(OffboardingProtocolPdfModel model)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(header =>
                        {
                            header.Item().Text(model.OrganizationName).FontSize(16).Bold();
                            header.Item().Text(model.OrganizationCountry).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(200).AlignRight().Column(header =>
                        {
                            header.Item().Text("Protokół zamknięcia offboardingu").FontSize(11).Bold();
                            header.Item().Text($"Nr protokołu: {model.ProtocolNumber}").FontColor(Colors.Grey.Darken1);
                        });
                    });
                    column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Column(person =>
                    {
                        person.Item().Text("Osoba").Bold();
                        person.Item().Text(model.PersonFullName);
                    });

                    column.Item().Row(row =>
                    {
                        if (model.StartedAt.HasValue) row.RelativeItem().Text($"Start offboardingu: {model.StartedAt:yyyy-MM-dd}");
                        row.RelativeItem().Text($"Termin zwrotu: {model.ReturnDueDate:yyyy-MM-dd}");
                        row.RelativeItem().Text($"Zamknięcie: {model.CompletedAt:yyyy-MM-dd}");
                    });

                    column.Item().Text($"Wynik: {model.FinalOutcome}").Bold();

                    if (model.Assets.Count > 0)
                    {
                        column.Item().Column(assetsSection =>
                        {
                            assetsSection.Item().Text("Sprzęt").Bold();
                            assetsSection.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCell).Text("Nazwa");
                                    header.Cell().Element(HeaderCell).Text("Tag");
                                    header.Cell().Element(HeaderCell).Text("Status");
                                    header.Cell().Element(HeaderCell).Text("Stan zwrotu");
                                    header.Cell().Element(HeaderCell).Text("Wykonał");
                                    header.Cell().Element(HeaderCell).Text("Data");

                                    static IContainer HeaderCell(IContainer container) => container
                                        .DefaultTextStyle(style => style.Bold())
                                        .PaddingBottom(4)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Darken1);
                                });

                                foreach (var asset in model.Assets)
                                {
                                    table.Cell().Element(BodyCell).Text(asset.Name);
                                    table.Cell().Element(BodyCell).Text(asset.AssetTag);
                                    table.Cell().Element(BodyCell).Text(asset.Status);
                                    table.Cell().Element(BodyCell).Text(asset.ReturnCondition ?? "—");
                                    table.Cell().Element(BodyCell).Text(asset.CompletedBy ?? "—");
                                    table.Cell().Element(BodyCell).Text(asset.CompletedAt?.ToString("yyyy-MM-dd") ?? "—");
                                }

                                static IContainer BodyCell(IContainer container) => container
                                    .PaddingVertical(4)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2);
                            });
                        });
                    }

                    if (model.ReleasedLicenses.Count > 0)
                    {
                        column.Item().Column(licensesSection =>
                        {
                            licensesSection.Item().Text("Zwolnione licencje").Bold();
                            licensesSection.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCell).Text("Licencja");
                                    header.Cell().Element(HeaderCell).Text("Data zwolnienia");
                                    header.Cell().Element(HeaderCell).Text("Wykonał");

                                    static IContainer HeaderCell(IContainer container) => container
                                        .DefaultTextStyle(style => style.Bold())
                                        .PaddingBottom(4)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Darken1);
                                });

                                foreach (var license in model.ReleasedLicenses)
                                {
                                    table.Cell().Element(BodyCell).Text(license.LicenseName);
                                    table.Cell().Element(BodyCell).Text(license.ReleasedAt?.ToString("yyyy-MM-dd") ?? "—");
                                    table.Cell().Element(BodyCell).Text(license.ReleasedBy ?? "—");
                                }

                                static IContainer BodyCell(IContainer container) => container
                                    .PaddingVertical(4)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2);
                            });
                        });
                    }

                    if (model.Exceptions.Count > 0)
                    {
                        column.Item().Column(exceptionsSection =>
                        {
                            exceptionsSection.Item().Text("Nierozliczone wyjątki").Bold();
                            foreach (var exception in model.Exceptions)
                            {
                                exceptionsSection.Item().Text($"• {exception.ItemLabel} — {exception.Status} ({exception.ResolvedBy}, {exception.ResolvedAt:yyyy-MM-dd})");
                                if (!string.IsNullOrWhiteSpace(exception.ResolutionNotes))
                                {
                                    exceptionsSection.Item().PaddingLeft(12).Text(exception.ResolutionNotes).FontColor(Colors.Grey.Darken1);
                                }
                            }
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(model.Notes))
                    {
                        column.Item().Column(notes =>
                        {
                            notes.Item().Text("Uwagi").Bold();
                            notes.Item().Text(model.Notes);
                        });
                    }

                    if (model.Photos.Count > 0)
                    {
                        column.Item().Column(photosSection =>
                        {
                            photosSection.Item().Text("Załącznik — materiał dowodowy").Bold();
                            foreach (var photo in model.Photos)
                            {
                                photosSection.Item().PaddingTop(6).Row(row =>
                                {
                                    row.ConstantItem(120).Height(90).Image(photo.Content).FitArea();
                                    row.RelativeItem().PaddingLeft(8).Column(details =>
                                    {
                                        details.Item().Text(photo.FileName);
                                        details.Item().Text($"SHA-256: {photo.Sha256}").FontSize(8).FontColor(Colors.Grey.Darken1);
                                    });
                                });
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Wygenerowano automatycznie przez Tenebit — ").FontColor(Colors.Grey.Darken1);
                    text.Span(model.ProtocolNumber).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }
}
