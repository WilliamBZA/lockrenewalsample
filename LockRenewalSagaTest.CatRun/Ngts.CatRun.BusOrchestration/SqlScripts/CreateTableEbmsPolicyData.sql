if (select object_id(N'nsb.EbmsPolicyData', N'U')) is null
    create table nsb.EbmsPolicyData (
        JobId          uniqueidentifier not null,
        EventId        int              not null,
        TaskId         int              null,
        TotalTaskCount int              null,

		--> optional columns
        Json           varchar(max)     null, --> in case more detail is desired
        CompleteDate   datetime         not null --default(getdate())
    );
