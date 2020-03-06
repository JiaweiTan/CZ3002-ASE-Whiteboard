﻿using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WhiteboardAPI.Helpers;
using WhiteboardAPI.Model;
using WhiteboardAPI.Repository;

namespace WhiteboardAPI.Controllers
{
    public class DiscussionBoardController : ControllerBase
    {
        private IDiscussionBoardRepository _discussionBoardRepository;
        private IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _accessor;
        private readonly LinkGenerator _generator;

        public DiscussionBoardController(IDiscussionBoardRepository discussionBoardRepository, IUserRepository userRepository, IMapper mapper, IHttpContextAccessor accessor, LinkGenerator generator)
        {
            _discussionBoardRepository = discussionBoardRepository;
            _userRepository = userRepository;
            _mapper = mapper;
            _accessor = accessor;
            _generator = generator;
        }

        private string CreateResourceUri(ResourceParameters resourceParameters, ResourceUriType type)
        {
            switch (type)
            {
                case ResourceUriType.PreviousPage:
                    return _generator.GetUriByPage(_accessor.HttpContext,
                        handler: null,
                        page: "/getAllPosts",
                        values:
                        new
                        {
                            orderBy = resourceParameters.OrderBy,
                            pageNumber = resourceParameters.PageNumber - 1,
                            pageSize = resourceParameters.PageSize
                        });
                case ResourceUriType.NextPage:
                    return _generator.GetUriByPage(_accessor.HttpContext,
                        handler: null,
                        page: "/getAllPosts",
                        values:
                        new
                        {
                            orderBy = resourceParameters.OrderBy,
                            pageNumber = resourceParameters.PageNumber + 1,
                            pageSize = resourceParameters.PageSize
                        });
                default:
                    return _generator.GetUriByPage(_accessor.HttpContext,
                        handler: null,
                        page: "/getAllPosts",
                        values:
                        new
                        {
                            orderBy = resourceParameters.OrderBy,
                            pageNumber = resourceParameters.PageNumber,
                            pageSize = resourceParameters.PageSize
                        });
            }
        }

        [AllowAnonymous]
        [HttpGet("getAllPosts")]
        public IActionResult GetAllPosts([FromQuery] ResourceParameters resourceParameters)
        {
            if (string.IsNullOrEmpty(resourceParameters.OrderBy))
                resourceParameters.OrderBy = "Title";

            var postsFromRepo = _discussionBoardRepository.GetAllPosts(resourceParameters);

            if (postsFromRepo == null)
            {
                return NotFound();
            }

            var previousPageLink = postsFromRepo.HasPrevious ? CreateResourceUri(resourceParameters, ResourceUriType.PreviousPage) : null;

            var x = CreateResourceUri(resourceParameters, ResourceUriType.NextPage);

            var nextPageLink = postsFromRepo.HasNext ? CreateResourceUri(resourceParameters, ResourceUriType.NextPage) : null;

            var paginationMetadata = new
            {
                totalCount = postsFromRepo.TotalCount,
                pageSize = postsFromRepo.PageSize,
                currentPage = postsFromRepo.CurrentPage,
                totalPages = postsFromRepo.TotalPages,
                previousPageLink = previousPageLink,
                nextPageLink = nextPageLink
            };

            Response.Headers.Add("X-Pagination", Newtonsoft.Json.JsonConvert.SerializeObject(paginationMetadata));

            return Ok(postsFromRepo);
        }

        [AllowAnonymous]
        [HttpPost("createPost")]
        public IActionResult CreatePost([FromBody] PostDto postDto)
        {
            if (!ModelState.IsValid)
            {
                // return 422
                return new Helpers.UnprocessableEntityObjectResult(ModelState);
            }

            try
            {
                // save
                var postFromRepo = _discussionBoardRepository.CreatePost(postDto);

                var postToReturn = _mapper.Map<PostDto>(postFromRepo);

                return Ok(postToReturn);
            }
            catch (AppException ex)
            {
                // return error message if there was an exception
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("getPost/{userId}")]
        public IActionResult GetPost(Guid userId, [FromQuery]Guid courseId, [FromQuery]string keyword)
        {
            var postsFromRepo = _discussionBoardRepository.GetPostsByUser(userId, courseId, keyword);

            if (postsFromRepo == null)
            {
                return NotFound();
            }

            var postDtos = _mapper.Map<IEnumerable<PostDto>>(postsFromRepo);

            foreach (PostDto p in postDtos)
            {
                var postUser = _userRepository.GetUser(p.CreatedBy);
                p.UserName = postUser.UserName;

                p.CourseFolderId = _discussionBoardRepository.GetPostFolders(p.PostId).Select(x => x.CourseFolderId).ToList();
                var replies = _discussionBoardRepository.GetReplies(p.PostId);
                if (replies != null)
                {
                    var userList = replies.Select(x => x.CreatedBy).ToList();
                    var users = _userRepository.GetUsers(userList);

                    p.LecturerReply = replies.Where(x => users.Where(x => x.Role.Equals( "lecturer", StringComparison.OrdinalIgnoreCase)).Select(x => x.UserId).ToList().Contains(x.CreatedBy)).Select(x => x.ReplyId).ToList();
                    p.StudentReply = replies.Where(x => users.Where(x => x.Role.Equals("student", StringComparison.OrdinalIgnoreCase)).Select(x => x.UserId).ToList().Contains(x.CreatedBy)).Select(x => x.ReplyId).ToList();

                }
            }

            return Ok(postDtos);
        }

        [AllowAnonymous]
        [HttpDelete("deletePost/{postId}")]
        public IActionResult DeletePost(Guid postId)
        {
            try
            {
                _discussionBoardRepository.DeletePost(postId);

                return Ok();
            }
            catch (AppException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("createReply")]
        public IActionResult CreateReply([FromBody] ReplyDto replyDto)
        {
            if (!ModelState.IsValid)
            {
                // return 422
                return new Helpers.UnprocessableEntityObjectResult(ModelState);
            }

            try
            {
                // save
                var replyFromRepo = _discussionBoardRepository.CreateReply(replyDto);

                var replyToReturn = _mapper.Map<ReplyDto>(replyFromRepo);

                return Ok(replyToReturn);
            }
            catch (AppException ex)
            {
                // return error message if there was an exception
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("getReply/{postId}")]
        public IActionResult GetReply(Guid postId)
        {
            var repliesFromRepo = _discussionBoardRepository.GetReplies(postId);

            if (repliesFromRepo == null)
            {
                return NotFound();
            }

            var replyDtos = _mapper.Map<IEnumerable<ReplyDto>>(repliesFromRepo);

            foreach (ReplyDto r in replyDtos)
            {
                var user = _userRepository.GetUser(r.CreatedBy);

                r.UserName = user.UserName;
            }

            return Ok(replyDtos);
        }

        [AllowAnonymous]
        [HttpDelete("deleteReply/{replyId}")]
        public IActionResult DeleteReply(Guid replyId)
        {
            try
            {
                _discussionBoardRepository.DeleteReply(replyId);

                return Ok();
            }
            catch (AppException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("createCourseFolder")]
        public IActionResult CreateCourseFolder([FromBody] CourseFolderDto courseFolderDto)
        {
            if (!ModelState.IsValid)
            {
                // return 422
                return new Helpers.UnprocessableEntityObjectResult(ModelState);
            }

            try
            {
                // save
                var cfFromRepo = _discussionBoardRepository.CreateCourseFolder(courseFolderDto);

                var cfToReturn = _mapper.Map<CourseFolderDto>(cfFromRepo);

                return Ok(cfToReturn);
            }
            catch (AppException ex)
            {
                // return error message if there was an exception
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("getCourseFolders/{courseId}")]
        public IActionResult GetGourseFolders(Guid courseId)
        {
            var foldersFromRepo = _discussionBoardRepository.GetCourseFolders(courseId);

            if (foldersFromRepo == null)
            {
                return NotFound();
            }

            var folderDtos = _mapper.Map<IEnumerable<CourseFolderDto>>(foldersFromRepo);

            return Ok(folderDtos);
        }

        [AllowAnonymous]
        [HttpGet("getPostFolders/{postId}")]
        public IActionResult GetPostFolders(Guid postId)
        {
            var foldersFromRepo = _discussionBoardRepository.GetPostFolders(postId);

            if (foldersFromRepo == null)
            {
                return NotFound();
            }

            var folderDtos = _mapper.Map<IEnumerable<PostFolderDto>>(foldersFromRepo);

            return Ok(folderDtos);
        }

        [AllowAnonymous]
        [HttpDelete("deleteCourseFolder/{courseFolderId}")]
        public IActionResult DeleteCourseFolder(Guid courseFolderId)
        {
            try
            {
                _discussionBoardRepository.DeleteCourseFolder(courseFolderId);

                return Ok();
            }
            catch (AppException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpDelete("deletePostFolder/{postFolderId}")]
        public IActionResult DeletePostFolderId(Guid postFolderId)
        {
            try
            {
                _discussionBoardRepository.DeletePostFolder(postFolderId);

                return Ok();
            }
            catch (AppException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
