# Bug log - block 15

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:00 | **WARN** | To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.
- day 1 08:00 | **REPEATING** | x25 and counting: Internal: JobTempAlloc has allocations that are more than the maximum lifespan of 4 frames old - this is not allowed and likely a leak
- day 1 08:00 | **REPEATING** | x25 and counting: To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.
- day 1 16:28 | **NO-TASK** | NO-TASK lane pallet 2A-2 sku=152431 load=9183167166
- day 1 17:43 | **NO-TASK** | NO-TASK lane pallet 2B-5 sku=152431 load=0572025675
- day 1 17:47 | **NO-TASK** | NO-TASK lane pallet 2B-5 sku=152431 load=0572025675
- day 1 18:38 | **NO-TASK** | NO-TASK lane pallet 1A-4 sku=482913 load=8058858671
