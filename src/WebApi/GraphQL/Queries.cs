﻿using Models.Entities.Timetables.Cells.CellMembers;
using Repository;

namespace WebApi.GraphQL
{
    public class Queries
    {
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        public IQueryable<Teacher> GetTeachers([Service] TimetableContext dbContext)
        {
            return dbContext.Set<Teacher>().AsQueryable();
        }
    }
}