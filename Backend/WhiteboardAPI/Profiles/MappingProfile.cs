﻿using AutoMapper;
using WhiteboardAPI.Entities;
using WhiteboardAPI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WhiteboardAPI.Profiles
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<UserDE, UserDto>();
            CreateMap<CourseDto, CourseDE>();
            CreateMap<CourseDE, CourseDto>();
            CreateMap<CourseStudentDE, CourseStudentDto>();
            CreateMap<CourseStaffDE, CourseStaffDto>();
            CreateMap<CourseStudentDto, CourseStudentDE>();
            CreateMap<CourseStaffDto, CourseStaffDE>();

        }
    }
}