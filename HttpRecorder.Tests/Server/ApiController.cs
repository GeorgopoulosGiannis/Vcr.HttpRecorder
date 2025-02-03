using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace HttpRecorder.Tests.Server
{
    [ApiController]
    public class ApiController : ControllerBase
    {
        public const string JsonUri = "json";
        public const string Windows1253EncodingUri = "windows1253";
        public const string FormDataUri = "formdata";
        public const string BinaryUri = "binary";
        public const string StatusCodeUri = "status";

        [HttpGet(JsonUri)]
        public IActionResult GetJson([FromQuery] string name = null)
            => Ok(new SampleModel { Name = name ?? SampleModel.DefaultName });

        [HttpPost(JsonUri)]
        public IActionResult PostJson(SampleModel model)
            => Ok(model);

        [HttpPost(FormDataUri)]
        public IActionResult PostFormData([FromForm] SampleModel model)
            => Ok(model);

        [HttpGet(BinaryUri)]
        public IActionResult GetBinary()
            => PhysicalFile(typeof(ApiController).Assembly.Location, "application/octet-stream");

        [HttpGet(StatusCodeUri)]
        public IActionResult GetStatus([FromQuery] HttpStatusCode? statusCode = HttpStatusCode.OK)
            => StatusCode((int)statusCode!.Value);

        [HttpGet(Windows1253EncodingUri)]
        public IActionResult GetWindows1253Encoding()
        {
            const string ResponseText = "Γειά σου κόσμε"; // Example Greek text
            var encoding = CodePagesEncodingProvider.Instance.GetEncoding(1253)!;
            var encodedBytes = encoding.GetBytes(ResponseText);
            var encodedString = encoding.GetString(encodedBytes);
            return new ContentResult
            {
                Content = $"{{\"message\":\"{encodedString}\"}}",
                ContentType = "application/json; charset=windows-1253",
                StatusCode = 200,
            };
        }
    }
}
