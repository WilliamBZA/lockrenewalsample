create or alter procedure nsb.ebmsGetRemaingTaskCount (
	@JobId            uniqueidentifier
	, @EventId        int
	, @RemainingCount int OUTPUT
)
as
begin
	declare
		@completedTasks int = (
			select count(1) - 1 from (
				select EventId, TaskId, TotalTaskCount
				from nsb.EbmsPolicyData
				where JobId = @JobId
					and EventId = @EventId
				group by EventId, TaskId, TotalTaskCount
			) t
		), @totalTasks int = (
			select top 1 TotalTaskCount
				from nsb.EbmsPolicyData
				where JobId = @JobId
					and EventId = @EventId
					and TaskId is null
		);

	set @RemainingCount = @totalTasks - @completedTasks;
end
