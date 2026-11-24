using System.Net.Mail;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Emailing;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Pins the Cc header the SMTP sender writes, read from the .eml file the service's own
    /// <see cref="SmtpClient"/> drops into a pickup directory, so the test sees the message as an SMTP
    /// server would receive it.
    /// </summary>
    public sealed class SmtpEmailingServiceCcTests : IDisposable
    {
        private readonly string _pickupDirectory = Directory.CreateTempSubdirectory("spiderly-smtp-cc-").FullName;

        public void Dispose() => Directory.Delete(_pickupDirectory, recursive: true);

        [Fact]
        public async Task A_cc_list_is_written_in_order_into_the_cc_header()
        {
            EmailingService service = NewServiceWritingTo(_pickupDirectory);

            await service.SendEmailAsync(
                "customer@example.com",
                "Subject",
                "<p>Body</p>",
                cc: ["second@example.com", "first@example.com"]);

            Assert.Equal("second@example.com, first@example.com", HeaderOfTheOneMessage("Cc"));
        }

        [Fact]
        public async Task The_send_with_attachments_writes_the_cc_header_too()
        {
            EmailingService service = NewServiceWritingTo(_pickupDirectory);

            await service.SendEmailAsync(
                "customer@example.com",
                "Subject",
                "<p>Body</p>",
                [EmailAttachment.FromBytes("order.pdf", [0x25, 0x50, 0x44, 0x46], "application/pdf")],
                cc: ["second@example.com", "first@example.com"]);

            Assert.Equal("second@example.com, first@example.com", HeaderOfTheOneMessage("Cc"));
        }

        [Fact]
        public async Task An_empty_cc_list_writes_no_cc_header()
        {
            EmailingService service = NewServiceWritingTo(_pickupDirectory);

            await service.SendEmailAsync("customer@example.com", "Subject", "<p>Body</p>", cc: []);

            Assert.Null(HeaderOfTheOneMessage("Cc"));
        }

        private EmailingService NewServiceWritingTo(string pickupDirectory)
        {
            EmailingService service = new(
                NullLogger<EmailingService>.Instance,
                Options.Create(new EmailOptions
                {
                    EmailSender = new EmailSender { Email = "no-reply@example.com" },
                    SmtpHost = "smtp.example.com",
                    SmtpEnableSsl = false, // SmtpClient refuses pickup-directory delivery with SSL on
                }));

            service.SmtpClient.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            service.SmtpClient.PickupDirectoryLocation = pickupDirectory;

            return service;
        }

        // A header longer than one line is folded onto indented continuation lines (RFC 5322 §2.2.3),
        // so the header block is unfolded before the named header is read.
        private string? HeaderOfTheOneMessage(string name)
        {
            string eml = File.ReadAllText(Assert.Single(Directory.GetFiles(_pickupDirectory, "*.eml")));
            string headerBlock = eml[..eml.IndexOf("\r\n\r\n", StringComparison.Ordinal)].Replace("\r\n ", " ").Replace("\r\n\t", " ");

            return headerBlock
                .Split("\r\n")
                .Where(line => line.StartsWith($"{name}: ", StringComparison.OrdinalIgnoreCase))
                .Select(line => line[(name.Length + 2)..])
                .SingleOrDefault();
        }
    }
}
