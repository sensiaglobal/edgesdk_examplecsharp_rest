#!/bin/bash

# Check if an IP address and appName are provided as command-line arguments
if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <ip_address> <appName>, e.g. ./webhookTest.sh 173.0.0.41 courseNetApp"
  exit 1
fi

ip_address="$1"
appName="$2"

while true; do
  # Generate a random integer between 1 and 10
  random_delay=$((RANDOM % 10 + 1))

  # Generate a timestamp (milliseconds since epoch) as a STRING
  timestamp="$(($(date +%s%3N)))"

  # Construct the JSON payload with the required tagsWriteReqs field, and timestamp as a string.
  payload="[{\"topic\": \"liveValue.postvalidConfig.this.$appName.0.configrunningperiod.\", \"value\": $random_delay, \"msgSource\": \"REST\", \"quality\": 192, \"timeStamp\": \"$timestamp\", \"tagsWriteReqs\": []}]"

  # Perform the curl request
  curl -X 'POST' \
    "http://$ip_address:7071/api/v1/message/write" \
    -H 'accept: */*' \
    -H 'Content-Type: application/json' \
    -d "$payload"

  # Display the random delay and timestamp for debugging
  echo "Delay: $random_delay seconds, Timestamp: $timestamp, IP: $ip_address"

  # Sleep for the random delay
  sleep "$random_delay"

done
