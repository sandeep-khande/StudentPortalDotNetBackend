using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.API.Data;
using StudentManagement.API.DTOs;
using StudentManagement.API.Models;
using System.Net.Mail;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace StudentManagement.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentsController : ControllerBase
    {
        private readonly StudentsManagementDbContext _context;

        public StudentsController(StudentsManagementDbContext context)
        {
            _context = context;
        }

        // 1. Register a Student
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] StudentRegisterDto studentDto)
        {

            // Map DTO to entity
            var student = new Students
            {
                FirstName = studentDto.FirstName,
                LastName = studentDto.LastName,
                Email = studentDto.Email,
                Password = studentDto.Password,
                CreatedDate = DateTime.UtcNow,
            };

            //checked if email already exists
            var existingStudent = await _context.Student.FirstOrDefaultAsync(s => s.Email == student.Email);
            if (existingStudent != null)
            {
                return BadRequest(new { message = "A Student with this email already exists."});
            }

            // Hash the password
            using var hmac = new HMACSHA256();
            student.Password = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(student.Password)));
            student.PasswordSalt = Convert.ToBase64String(hmac.Key);

            // Add student to database
            student.CreatedDate = DateTime.UtcNow;
            _context.Student.Add(student);
            await _context.SaveChangesAsync();

            return Ok(new {message = "Registration successful"});
        }

        //2. Log in a student
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] StudentLoginDto loginDto)
        {
            var existingStudent = await _context.Student.FirstOrDefaultAsync(s => s.Email == loginDto.Email);
            if (existingStudent == null)
            {
                return Unauthorized("Inavalid email or password.");
            }

            // Validate the password
            using var hmac = new HMACSHA256(Convert.FromBase64String(existingStudent.PasswordSalt));
            var computedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDto.Password)));
            if (computedHash != existingStudent.Password)
            {
                return Unauthorized("Incorrect Password");
            }

            return Ok(new
            {
                message = "Login successful",
                studentId = existingStudent.Id,
                
            });
        }

        // 3. Retrieve student profile
        [HttpGet("StudentDetails/{id}")]
        public async Task<IActionResult> GetStudentProfile(int id)
        {
            var student = await _context.Student.FindAsync(id);
            if (student == null)
            {
                return NotFound("Student not found");
            }

            var studentDetailsDto = new StudentDetailsDto
            {
                Id = student.Id,
                FirstName = student.FirstName,
                LastName = student.LastName,
                Email = student.Email,
            };

            return Ok(studentDetailsDto);


        }

        // 4. Update student profile
        [HttpPut("UpdateStudentDetails/{id}")]
        public async Task<IActionResult> UpdateStudentProfile(int id, [FromBody] UpdateStudentDto updatedStudentDto)
        {
            var existingStudent = await _context.Student.FindAsync(id);
            if (existingStudent == null)
            {
                return NotFound("Student not found");
            }

            // update fields
            

            if (!string.IsNullOrEmpty(updatedStudentDto.Password))
            {
                using var hmac = new HMACSHA256();

                existingStudent.Password = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(updatedStudentDto.Password)));

                existingStudent.PasswordSalt = Convert.ToBase64String(hmac.Key);
            }

            await _context.SaveChangesAsync();
            //return Ok("Profile updated successfully");

            return Ok(new { message = "Profile updated successfully" });


        }

        // 5. Delete a student
        [HttpDelete("DeleteStudent/{id}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _context.Student.FindAsync(id);
            if (student == null)
            {
                return NotFound("Student not found.");
            }

            _context.Student.Remove(student);
            await _context.SaveChangesAsync();
            return Ok("Student deleted successfully");
        }

        //Generating Reset Link
        [HttpPost("generate-reset-link")]
        [Consumes("text/plain", "application/json")]
        public async Task<IActionResult> GenerateResetLink([FromBody] string email)
        {
            var student = await _context.Student.FirstOrDefaultAsync(s => s.Email == email);
            if (student == null)
            {
                return NotFound("No student found with the provided email.");
            }

            // Generate a unique token
            var resetToken = Guid.NewGuid().ToString();
            student.ResetToken = resetToken;
            student.ResetTokenExpiry = DateTime.UtcNow.AddHours(1); // Token valid for 1 hour
            await _context.SaveChangesAsync();

            // Generate the reset link
            var resetLink = $"http://localhost:4200/reset-password?token={resetToken}";

            // Send the reset link via email
            await SendResetEmail(student.Email, resetLink);

            return Ok(new { message = "Password reset link has been sent to your email." });
        }

        private async Task SendResetEmail(string email, string resetLink)
        {
            // SMTP configuration
            var smtpServer = "smtp.gmail.com";
            var smtpPort = 587;
            var senderEmail = "sandeepkhande02@gmail.com";
            var senderPassword = "znvw xzdw qfnc dfzx"; // Use App Password if 2FA is enabled

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, "Your App Name"),
                Subject = "Password Reset Request",
                Body = $"<p>Click <a href=\"{resetLink}\">here</a> to reset your password.</p>",
                IsBodyHtml = true
            };
            mailMessage.To.Add(email);

            using var smtpClient = new SmtpClient(smtpServer, smtpPort)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            try
            {
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                throw;
            }
        }

        // Reset Password Endpoint
        [HttpPut("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            var student = await _context.Student.FirstOrDefaultAsync(s => s.ResetToken == resetPasswordDto.Token);
            if (student == null || student.ResetTokenExpiry < DateTime.UtcNow)
            {
                return BadRequest("Invalid or expired reset token.");
            }

            // Hash and save the new password
            using var hmac = new HMACSHA256();
            student.Password = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(resetPasswordDto.Password)));
            student.PasswordSalt = Convert.ToBase64String(hmac.Key);

            // Clear the reset token
            student.ResetToken = null;
            student.ResetTokenExpiry = null;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Password has been reset successfully." });
        }


    }
}
