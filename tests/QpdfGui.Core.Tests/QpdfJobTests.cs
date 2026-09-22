using QpdfGui.Core.Jobs;
using Xunit;

namespace QpdfGui.Core.Tests;

public class QpdfJobTests
{
    [Fact]
    public void ToCommandLine_EmptyMergeJob_ProducesCorrectCommand()
    {
        var job = new QpdfJob
        {
            Empty = "",
            Pages = new List<PagesSpec>
            {
                new() { File = "doc1.pdf", Range = "1-5" },
                new() { File = "doc 2.pdf", Range = "z-1" }
            },
            OutputFile = "merged.pdf"
        };

        var cmd = job.ToCommandLine();
        Assert.Equal("qpdf --empty --pages doc1.pdf 1-5 \"doc 2.pdf\" z-1 -- merged.pdf", cmd);
    }

    [Fact]
    public void ToCommandLine_EncryptJob_ProducesCorrectCommand()
    {
        var job = new QpdfJob
        {
            InputFile = "plain.pdf",
            OutputFile = "enc.pdf",
            Encrypt = new EncryptOptions
            {
                UserPassword = "u1",
                OwnerPassword = "o1",
                Aes256 = new Encrypt256BitOptions
                {
                    Print = "none",
                    Modify = "none",
                    Extract = "n"
                }
            }
        };

        var cmd = job.ToCommandLine();
        Assert.Equal("qpdf plain.pdf --encrypt u1 o1 256 --print=none --modify=none --extract=n -- enc.pdf", cmd);
    }

    [Fact]
    public void ToCommandLine_DecryptJob_ProducesCorrectCommand()
    {
        var job = new QpdfJob
        {
            Password = "secret password",
            Decrypt = "",
            InputFile = "enc.pdf",
            OutputFile = "plain.pdf"
        };

        var cmd = job.ToCommandLine();
        Assert.Equal("qpdf --password=\"secret password\" --decrypt enc.pdf plain.pdf", cmd);
    }

    [Fact]
    public void ToCommandLine_RotateJob_ProducesCorrectCommand()
    {
        var job = new QpdfJob
        {
            InputFile = "in.pdf",
            OutputFile = "out.pdf",
            Rotate = new List<string> { "+90:1-z" }
        };

        var cmd = job.ToCommandLine();
        Assert.Equal("qpdf in.pdf --rotate=+90:1-z out.pdf", cmd);
    }

    [Fact]
    public void ToCommandLine_SplitPagesJob_ProducesCorrectCommand()
    {
        var job = new QpdfJob
        {
            InputFile = "in.pdf",
            OutputFile = "split_%d.pdf",
            SplitPages = "2"
        };

        var cmd = job.ToCommandLine();
        Assert.Equal("qpdf in.pdf --split-pages=2 split_%d.pdf", cmd);
    }

    [Fact]
    public void ToCommandLine_LinearizeRepairJob_ProducesCorrectCommand()
    {
        var job = new QpdfJob
        {
            InputFile = "in.pdf",
            OutputFile = "out.pdf",
            Linearize = ""
        };

        var cmd = job.ToCommandLine();
        Assert.Equal("qpdf --linearize in.pdf out.pdf", cmd);
    }

    [Fact]
    public void ToJson_SerializesCorrectly()
    {
        var job = new QpdfJob
        {
            InputFile = "a.pdf",
            OutputFile = "b.pdf",
            Linearize = ""
        };

        var json = job.ToJson();
        Assert.Contains("\"inputFile\": \"a.pdf\"", json);
        Assert.Contains("\"outputFile\": \"b.pdf\"", json);
        Assert.Contains("\"linearize\": \"\"", json);
    }
}
