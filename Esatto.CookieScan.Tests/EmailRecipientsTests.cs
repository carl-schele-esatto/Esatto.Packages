using Esatto.CookieScan.Engine;

namespace Esatto.CookieScan.Tests;

public class EmailRecipientsTests
{
    [Fact]
    public void An_empty_field_names_nobody()
    {
        Assert.Empty(EmailRecipients.Parse(null));
        Assert.Empty(EmailRecipients.Parse(""));
        Assert.Empty(EmailRecipients.Parse("   "));
    }

    [Fact]
    public void One_address_is_one_recipient()
        => Assert.Equal(["legal@client.se"], EmailRecipients.Parse("legal@client.se"));

    // Comma and semicolon both, because both are what a mail client puts between addresses and an
    // operator pastes whichever their own client produced.
    [Theory]
    [InlineData("a@x.se,b@y.se")]
    [InlineData("a@x.se; b@y.se")]
    [InlineData("a@x.se , b@y.se")]
    [InlineData("a@x.se\nb@y.se")]
    [InlineData("a@x.se\r\nb@y.se")]
    public void Every_separator_splits_the_same_way(string typed)
        => Assert.Equal(["a@x.se", "b@y.se"], EmailRecipients.Parse(typed));

    [Fact]
    public void Padding_is_trimmed_off_every_address()
        => Assert.Equal(["a@x.se", "b@y.se"], EmailRecipients.Parse("  a@x.se ,   b@y.se  "));

    [Fact]
    public void An_empty_entry_between_separators_is_dropped()
        => Assert.Equal(["a@x.se", "b@y.se"], EmailRecipients.Parse("a@x.se,,;  ,b@y.se"));

    // A domain is case-insensitive, so the same person listed twice in two spellings would otherwise
    // be mailed twice. The first spelling is kept: it is the one that was typed first.
    [Fact]
    public void The_same_address_in_two_spellings_is_one_recipient()
        => Assert.Equal(["Legal@Client.se"], EmailRecipients.Parse("Legal@Client.se, legal@client.se"));

    [Fact]
    public void The_order_typed_is_the_order_kept()
        => Assert.Equal(["c@x.se", "a@x.se", "b@x.se"], EmailRecipients.Parse("c@x.se, a@x.se, b@x.se"));

    // Deliberately no validation here - there is one rule for what a mailbox is, and it is the one
    // MailKit applies when the message is built. A second grammar in this class would mean an address
    // the mail server would have accepted, dropped with nothing to show for it.
    [Fact]
    public void Something_that_is_not_an_address_is_still_passed_along_to_be_refused_later()
        => Assert.Equal(["not-an-address"], EmailRecipients.Parse("not-an-address"));
}
