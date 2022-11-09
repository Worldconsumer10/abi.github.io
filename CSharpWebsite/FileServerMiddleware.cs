using System.Net;
using System.Text;

namespace CSharpWebsite
{
    public class FileServerMiddleware
    {
        private readonly RequestDelegate _next;
        public FileServerMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        public static async Task ReplyFile(HttpContext context,string relative_path)
        {
            var file = new FileInfo(Environment.CurrentDirectory+"/"+relative_path);
            byte[] buffer;
            if (file.Exists)
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "text/html";

                buffer = File.ReadAllBytes(file.FullName);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.ContentType = "text/plain";
                buffer = Encoding.UTF8
                    .GetBytes("Unable to find the requested file");
            }

            context.Response.ContentLength = buffer.Length;

            using (var stream = context.Response.Body)
            {
                await stream.WriteAsync(buffer, 0, buffer.Length);
                await stream.FlushAsync();
            }
            return;
        }
    }
}
