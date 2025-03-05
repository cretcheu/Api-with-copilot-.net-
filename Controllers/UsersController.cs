using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace UserManagementAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private static Dictionary<int, User> users = new Dictionary<int, User>
        {
            { 1, new User { Id = 1, Name = "John Doe", Email = "john.doe@example.com", Password = "password123" } },
            { 2, new User { Id = 2, Name = "Jane Smith", Email = "jane.smith@example.com", Password = "password456" } }
        };

        // GET: api/users
        [HttpGet]
        public ActionResult<IEnumerable<User>> Get()
        {
            return users.Values.ToList();
        }

        // GET: api/users/{id}
        [HttpGet("{id}")]
        public ActionResult<User> Get(int id)
        {
            if (!users.ContainsKey(id))
            {
                return NotFound(new { Message = $"User with ID {id} not found." });
            }
            return users[id];
        }

        // POST: api/users
        [HttpPost]
        public ActionResult<User> Post([FromBody] User user)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (users.Values.Any(u => u.Email == user.Email))
            {
                return Conflict(new { Message = "A user with the same email already exists." });
            }

            user.Id = users.Keys.Max() + 1;
            users.Add(user.Id, user);
            return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
        }

        // PUT: api/users/{id}
        [HttpPut("{id}")]
        public IActionResult Put(int id, [FromBody] User updatedUser)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!users.ContainsKey(id))
            {
                return NotFound(new { Message = $"User with ID {id} not found." });
            }

            if (users.Values.Any(u => u.Email == updatedUser.Email && u.Id != id))
            {
                return Conflict(new { Message = "A user with the same email already exists." });
            }

            var user = users[id];
            user.Name = updatedUser.Name;
            user.Email = updatedUser.Email;
            user.Password = updatedUser.Password;
            return Ok(new { Message = "User updated successfully." });
        }

        // DELETE: api/users/{id}
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            if (!users.ContainsKey(id))
            {
                return NotFound(new { Message = $"User with ID {id} not found." });
            }

            users.Remove(id);
            return Ok(new { Message = "User deleted successfully." });
        }
    }

    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }
    }
}
