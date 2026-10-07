using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using api_gateway.Models.DTOs;

namespace api_gateway.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly TableClient _usersTable;
    private readonly IConfiguration _configuration;

    public AuthController(TableClient usersTable, IConfiguration configuration)
    {
        _usersTable = usersTable;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] Models.DTOs.RegisterRequest request)
    {
        // ---------------------------------------------
        // Basic validation
        // ---------------------------------------------

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new
            {
                message = "Email, password and full name are required."
            });
        }

        var email = request.Email.Trim().ToLowerInvariant();

        // ---------------------------------------------
        // Check whether the email already exists.
        //
        // Because Role is the PartitionKey, we check
        // each valid role partition.
        // ---------------------------------------------

        var roles = new[]
        {
            "User",
            "Admin"
        };

        foreach (var role in roles)
        {
            try
            {
                await _usersTable.GetEntityAsync<UserEntity>(
                    role,
                    email);

                // If GetEntityAsync succeeds, the user exists.

                return Conflict(new
                {
                    message =
                        "A user with this email already exists."
                });
            }
            catch (RequestFailedException ex)
                when (ex.Status == 404)
            {
                // User does not exist in this partition.
                // Continue checking.
            }
        }

        // ---------------------------------------------
        // Hash password
        // ---------------------------------------------

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // ---------------------------------------------
        // Create user
        //
        // Public registration always creates Customer.
        // ---------------------------------------------

        var user = new UserEntity
        {
            PartitionKey = "User",
            RowKey = email,

            FullName = request.FullName.Trim(),

            PasswordHash = passwordHash,

            CreatedDate = DateTime.UtcNow
        };

        // ---------------------------------------------
        // Save to Azure Table Storage
        // ---------------------------------------------

        await _usersTable.AddEntityAsync(user);

        // Never return PasswordHash.

        return StatusCode(
            StatusCodes.Status201Created,
            new
            {
                email = user.RowKey,
                fullName = user.FullName,
                role = user.PartitionKey,
                createdDate = user.CreatedDate
            });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] Models.DTOs.LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||  string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var email = request.Email
            .Trim()
            .ToLowerInvariant();

        UserEntity? user = null;

        var roles = new[]
        {
            "User",
            "Admin"
        };

        foreach (var role in roles)
        {
            try
            {
                var response =
                    await _usersTable.GetEntityAsync<UserEntity>(role,email);

                user = response.Value;

                break;
            }
            catch (RequestFailedException ex)
                when (ex.Status == 404)
            {
                // Not in this partition.
            }
        }

        // ---------------------------------------------
        // User not found
        // ---------------------------------------------

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        // ---------------------------------------------
        // Verify password
        // ---------------------------------------------

        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!passwordValid)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        // ---------------------------------------------
        // Create claims
        // ---------------------------------------------

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.Name,
                user.FullName),

            new(
                ClaimTypes.Email,
                user.RowKey),

            new(
                ClaimTypes.Role,
                user.PartitionKey)
        };

        // ---------------------------------------------
        // Read JWT configuration
        // ---------------------------------------------

        var key = _configuration["Jwt:Key"];

        var issuer = _configuration["Jwt:Issuer"];

        var audience = _configuration["Jwt:Audience"];

        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");

        // ---------------------------------------------
        // Create signing credentials
        // ---------------------------------------------

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(securityKey,SecurityAlgorithms.HmacSha256);

        // ---------------------------------------------
        // Generate JWT
        // ---------------------------------------------

        var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var token =
            new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiry,
                signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        // ---------------------------------------------
        // Return token
        // ---------------------------------------------

        return Ok(new
        {
            token = tokenString,
            expires = expiry,

            user = new
            {
                email = user.RowKey,
                fullName = user.FullName,
                role = user.PartitionKey
            }
        });
    }
}