using System.Text;
using FluentAssertions;
using NotificationHub.Core.Campaigns;

namespace NotificationHub.Core.Tests.Campaigns;

public class CsvRecipientParserTests
{
    [Fact]
    public async Task Parses_Phone_Header()
    {
        var csv = "PhoneNumber,Name\n+989121111111,Ali\n+989122222222,Sara\n";
        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var list = await CsvRecipientParser.ParseAddressesAsync(ms);
        list.Should().HaveCount(2);
        list[0].Should().Be("+989121111111");
    }

    [Fact]
    public async Task Parses_Headerless_Single_Column()
    {
        var csv = "+989121111111\nuser@example.com\n";
        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var list = await CsvRecipientParser.ParseAddressesAsync(ms);
        list.Should().Contain("+989121111111");
    }

    [Fact]
    public void NormalizeHeader_Strips_Underscore()
    {
        CsvRecipientParser.NormalizeHeader("Phone_Number").Should().Be("phonenumber");
    }
}
