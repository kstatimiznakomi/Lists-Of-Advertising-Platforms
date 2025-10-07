using Microsoft.AspNetCore.Mvc;
using StoreAndReturnListsOfAdvertisingPlatforms.Service;

namespace StoreAndReturnListsOfAdvertisingPlatforms.Controller;

[ApiController]
[Route("api/ads")]
public class AdsController(LocationAdvertiserService service) : ControllerBase{
    [HttpPost]
    public async Task<IActionResult> Upload(){
        try{
            var contentType = Request.ContentType ?? "(null)";
            var contentLength = Request.ContentLength;
            var form = await Request.ReadFormAsync();
            
            if (Request is{ HasFormContentType: true, Form.Files.Count: > 0 }){
                var file = Request.Form.Files[0];
                using var sr = new StreamReader(file.OpenReadStream());
                var text = await sr.ReadToEndAsync();
                
                service.LoadFromText(text);

                return Ok(new{
                    message = "Загружено в память",
                });
            }
                // возможно клиент отправил form-data, но без файла
                // попробуем найти текстовые поля
                var textField = form.Files.Count == 0 && form.TryGetValue("text", out var val) ? val.ToString() : null;
                if (string.IsNullOrWhiteSpace(textField))
                    return BadRequest(new{ error = "Не предоставлено никакого файла или содержимого", contentType, contentLength });
                service.LoadFromText(textField);
                return Ok(new { message = "Текст из поля формы загружен", contentType, contentLength });
                // нет файлов и нет поля text — продолжим ниже читать raw body
        }
        catch (Exception ex){
            Console.WriteLine(ex);
            return StatusCode(500, new { error = "Ошибка при сохранении файла. Попробуйте позже." });
        }
        
    }

    
    // GET api/ads/search?location=/ru/svrd/revda
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string location){
        try{
            if (string.IsNullOrWhiteSpace(location)) return BadRequest(new { error = "Локация не задана" });
            location = service.NormalizeLocation(location);
            var advertisers = service.Search(location) ?? Enumerable.Empty<string>();
            var result = advertisers.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
            return Ok(result);
        }
        catch (Exception ex){
            Console.WriteLine(ex);
            return StatusCode(500, new { error = "Ошибка при поиске площадки. Попробуйте позже." });
        }
    }
}