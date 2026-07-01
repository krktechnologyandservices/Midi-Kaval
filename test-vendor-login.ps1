$body = @{ email = "karthik.k.82@outlook.com"; password = "Rivisha1407@" } | ConvertTo-Json
$loginResult = Invoke-RestMethod -Uri "http://localhost:5049/api/v1/auth/login" -Method Post -Body $body -ContentType "application/json"
Write-Host "Login response:"
$loginResult | ConvertTo-Json
$challengeId = $loginResult.data.challengeId
Write-Host "`nChallengeId: $challengeId"

# Get OTP from Redis
$otpHash = docker exec midi-kaval-redis redis-cli HGET "otp:challenge:$challengeId" otp_hash
Write-Host "OTP Hash from Redis: $otpHash"

Write-Host "`nDebug: Checking Redis challenge data..."
docker exec midi-kaval-redis redis-cli HGETALL "otp:challenge:$challengeId"
