create or alter procedure nsb.ebmsClearEventTasks (
	@JobId        uniqueidentifier = null
	, @EventId    int = null
	, @ExpireDays int = null
) as
begin
	if @JobId is not null
	begin
		if @EventId is null
			delete nsb.EbmsPolicyData
			where JobId = @JobId;
		else
			delete nsb.EbmsPolicyData
			where JobId = @JobId
				and EventId = @EventId;
	end
    if @ExpireDays is not null
		delete nsb.EbmsPolicyData
		where datediff(d, CompleteDate, getdate()) > @ExpireDays;
end
