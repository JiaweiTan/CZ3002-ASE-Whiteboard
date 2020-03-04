﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WhiteboardAPI.Helpers;
using WhiteboardAPI.Model;
using WhiteboardAPI.Repository;
using WhiteboardAPI.Entities;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WhiteboardAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {

        private IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly AppSettings _appSettings;
        private readonly IEmailSender _emailSender;
        private readonly IUrlHelper _urlHelper;

        public UserController(IUserRepository userRepository, IMapper mapper, IUrlHelper urlHelper, IOptions<AppSettings> appSettings, IEmailSender emailSender)
        {
            _userRepository = userRepository;
            _mapper = mapper;
            _appSettings = appSettings.Value;
            _emailSender = emailSender;
            _urlHelper = urlHelper;
        }

        [AllowAnonymous]
        [HttpPost("authenticate")]
        public IActionResult Authenticate([FromBody] UserAuthenticationDto userDto)
        {
            var user = _userRepository.Authenticate(userDto.Username, userDto.Password);

            if (user == null)
                return BadRequest(new { message = "Username or password is incorrect" });

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, user.UserId.ToString()),
                    new Claim(ClaimTypes.Role, user.Role)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);

            var userFromRepo = _mapper.Map<UserDto>(user);

            userFromRepo.Token = tokenHandler.WriteToken(token);

            return Ok(userFromRepo);
        }

        [AllowAnonymous]
        [HttpPost("createUser")]
        public IActionResult Register(UserCreationDto userCreationDto)
        {
            if (!ModelState.IsValid)
            {
                // return 422
                return new Helpers.UnprocessableEntityObjectResult(ModelState);
            }

            try
            {
                // save
                var userFromRepo = _userRepository.Create(userCreationDto);

                var userToReturn = _mapper.Map<UserDto>(userFromRepo);

                return Ok(userToReturn);
            }
            catch (AppException ex)
            {
                // return error message if there was an exception
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("getUsers")]
        public IActionResult GetUsers([FromQuery] List<Guid> userIds)
        {
            var usersFromRepo = _userRepository.GetUsers(userIds);

            if (usersFromRepo == null)
            {
                return NotFound();
            }

            IEnumerable<UserDto> userDtos = _mapper.Map<IEnumerable<UserDto>>(usersFromRepo);

            Users users = new Users
            {
                UserDtos = userDtos
            };

            users.TotalCount = userDtos.Count();

            return Ok(users);
        }

        [AllowAnonymous]
        [HttpPut("{id}")]
        public IActionResult UpdateUser(Guid id, [FromBody]UserUpdateDto userUpdateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // return 422
                    return new Helpers.UnprocessableEntityObjectResult(ModelState);
                }

                if (!_userRepository.UserExists(id))
                {
                    return NotFound();
                }

                // map dto to entity and set id
                var user = _mapper.Map<UserDE>(userUpdateDto);

                _userRepository.UpdateUser(id, user);

                if (!_userRepository.Save())
                {
                    throw new AppException("Updating {id} for user failed on save.");
                }

                return NoContent();
            }
            catch (AppException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}