﻿CREATE TABLE tbl_db_post (    PostId uniqueidentifier NOT NULL PRIMARY KEY,    CourseId uniqueidentifier NOT NULL FOREIGN KEY(CourseId) REFERENCES tbl_course(CourseId),    Title nvarchar(255),    Description nvarchar(255),    CreatedOn datetime,    CreatedBy uniqueidentifier);