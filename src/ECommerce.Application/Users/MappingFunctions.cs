using ECommerce.Application.Users.Dtos;
using ECommerce.Domain.Entities;
using Mapster;
using MapsterMapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Application.Users
{
    public sealed class MappingFunctions
    {
        public static UserDto CustomMapUserToUserDto(User user)
        {
            UserDto userDto = user.Adapt<UserDto>();
            return userDto;
        }
    }
}
