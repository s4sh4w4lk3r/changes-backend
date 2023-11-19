﻿using Models.Entities.Timetables.Cells;

namespace WebApi.GraphQL.Types.Timetables
{
    public class ActualTimetableCellType : ObjectType<ActualTimetableCell>
    {
        protected override void Configure(IObjectTypeDescriptor<ActualTimetableCell> descriptor)
        {
            descriptor.Field(e => e.TimetableCellId);

            descriptor.Field(e => e.IsCanceled);
            descriptor.Field(e => e.IsModified);
            descriptor.Field(e => e.IsMoved);

            descriptor.Field(e => e.Cabinet).Type<NonNullType<CabinetType>>();
            descriptor.Field(e => e.CabinetId);

            descriptor.Field(e => e.Teacher).Type<NonNullType<LessonTimeType>>();
            descriptor.Field(e => e.TeacherId);

            descriptor.Field(e => e.Subject).Type<NonNullType<SubjectType>>();
            descriptor.Field(e => e.SubjectId);

            descriptor.Field(e => e.LessonTime).Type<NonNullType<LessonTimeType>>();
            descriptor.Field(e => e.LessonTimeId);

            descriptor.Field(e => e.Date).Type<NonNullType<StringType>>();
            descriptor.Field(e => e.SubGroup).Type<SubGroupType>();
        }
    }
}
