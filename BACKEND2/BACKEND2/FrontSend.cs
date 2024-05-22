using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BACKEND2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FormController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public FormController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost]
        public async Task<IActionResult> PostData([FromForm] FormData formData)
        {
            try
            {
                // Проверяем, существует ли уже номер телефона в базе данных
                var phoneNumberExists = await _dbContext.Users
                    .AnyAsync(u => u.PhoneNumber == formData.PhoneNumber);

                if (phoneNumberExists)
                {
                    return Conflict(new { message = "Phone number already exists" });
                }

                // Создаем новую сущность User
                var user = new User
                {
                    Country = formData.Country,
                    PhoneNumber = formData.PhoneNumber,
                    Password = formData.Password // предполагается, что пароль хешируется и т.д.
                };

                // Добавляем пользователя в контекст данных
                _dbContext.Users.Add(user);

                // Сохраняем изменения в базе данных
                await _dbContext.SaveChangesAsync();

                return Ok(new { Message = "Data saved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { ErrorMessage = "An error occurred while saving data", ErrorDetails = ex.Message });
            }
        }
    }

    public class FormData
    {
        public string Country { get; set; }
        public string PhoneNumber { get; set; }
        public string Password { get; set; }
    }
}
