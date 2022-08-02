create or alter procedure nsb.ebmsInsertTaskProgress (
	@JobId            uniqueidentifier
	, @EventId        int
	, @TaskId         int = null
	, @TotalTaskCount int = null
	, @Json           varchar(max) = null
) as
begin
	insert into nsb.EbmsPolicyData
	values (@JobId, @EventId, @TaskId, @TotalTaskCount, @json, getdate());
end
