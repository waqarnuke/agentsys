using API.Data;
using API.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MembersController(AppDbContext context) : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<AppUser>> GetMember()
        {
            var user = context.Users.ToList();
            return Ok(user);
        }

        [HttpGet("{id}")]
        public ActionResult<AppUser> GetMember(string id)
        {
            var user = context.Users.Find(id);

            if (user == null) return NotFound("User not found");

            return Ok(user);
        }   
    }
}
