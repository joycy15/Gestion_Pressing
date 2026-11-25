using System;
using System.Drawing;
using System.Drawing.Printing;
using Pressing.BLL;
using Pressing.Models;

namespace Pressing.UI.Printing;

public sealed class TicketPrintService
{
    private readonly TicketDocumentFormatter _formatter;

    public TicketPrintService()
        : this(new TicketDocumentFormatter())
    {
    }

    public TicketPrintService(TicketDocumentFormatter formatter)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    public PrintDocument ConstruireDocument(TicketDepotDto ticket)
    {
        if (ticket is null)
        {
            throw new ArgumentNullException(nameof(ticket));
        }

        string contenu = _formatter.FormaterTexteTicket(ticket);
        PrintDocument document = new()
        {
            DocumentName = $"Ticket_{ticket.NumeroTicket}"
        };

        document.PrintPage += (_, e) =>
        {
            using Font police = new("Consolas", 10f, FontStyle.Regular, GraphicsUnit.Point);
            e.Graphics?.DrawString(
                contenu,
                police,
                Brushes.Black,
                new RectangleF(
                    e.MarginBounds.Left,
                    e.MarginBounds.Top,
                    e.MarginBounds.Width,
                    e.MarginBounds.Height));
            e.HasMorePages = false;
        };

        return document;
    }

    public void ImprimerTicket(TicketDepotDto ticket, bool afficherApercu = false)
    {
        using PrintDocument document = ConstruireDocument(ticket);
        if (afficherApercu)
        {
            using PrintPreviewDialog apercu = new()
            {
                Document = document,
                Width = 600,
                Height = 800
            };
            apercu.ShowDialog();
            return;
        }

        document.Print();
    }
}
