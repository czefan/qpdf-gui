using System.Text.Json;
using QpdfGui.Core.Jobs;
using Xunit;

namespace QpdfGui.Core.Tests;

public class JobSerializationTests
{
    [Fact]
    public void QpdfJob_SerializesExpectedJson_OmitsNulls()
    {
        var job = new QpdfJob
        {
            InputFile = "--empty",
            OutputFile = "output.pdf",
            Pages =
            [
                new PagesSpec { File = "a.pdf", Range = "1-5" },
                new PagesSpec { File = "b.pdf", Range = "z-1" }
            ],
            Progress = ""
        };

        var json = job.ToJson();

        Assert.Contains("\"inputFile\": \"--empty\"", json);
        Assert.Contains("\"outputFile\": \"output.pdf\"", json);
        Assert.Contains("\"file\": \"a.pdf\"", json);
        Assert.Contains("\"range\": \"1-5\"", json);
        Assert.Contains("\"progress\": \"\"", json);

        // 未赋值属性不应出现在 JSON 中
        Assert.DoesNotContain("\"splitPages\"", json);
        Assert.DoesNotContain("\"encrypt\"", json);
        Assert.DoesNotContain("\"decrypt\"", json);
        Assert.DoesNotContain("\"rotate\"", json);
    }

    [Fact]
    public void EncryptOptions_Serializes256BitKeyCorrectly()
    {
        var job = new QpdfJob
        {
            InputFile = "in.pdf",
            OutputFile = "out.pdf",
            Encrypt = new EncryptOptions
            {
                UserPassword = "pwd",
                Aes256 = new Encrypt256BitOptions
                {
                    Print = "none",
                    Extract = "n"
                }
            }
        };

        var json = job.ToJson();

        Assert.Contains("\"256bit\"", json);
        Assert.Contains("\"print\": \"none\"", json);
        Assert.Contains("\"extract\": \"n\"", json);
        Assert.Contains("\"userPassword\": \"pwd\"", json);
    }
}
