# Batch Transaction Preprocessing
## Overview
This service polls the Exavault FTP server nightly for a CSV file from the previous day. This can be overridden by providing a date in yyyy-MM-dd format as a command line argument. 

## Local Environment
You will need Docker Desktop installed to build and run the service locally. To upload the docker image to ECR, you will need AWS CLI V2 installed. For both scenarios, an IAM credential set or role must be present on the machine, with at least SSM read access, S3 write access, and SQS write access.

## Build
After cloning this repo, from the root of the directory run ```docker build cs301-batch . ``` to build the docker image.

## Run Locally
To run the docker image locally, run ```docker run cs301-batch 2021-09-19``` to run it using the specified date, or omit the date to use the previous day's date by default.

## Upload to ECR
On any successful push or merge request to the main branch, a github action will be triggered, which automatically builds an updated container image and pushes it to ECR. Some secret variables must first be specified in **Settings** -> **Secrets**

## Batch Configuration
To set up batch to run the docker image, configure a compute environment with your desired resources and an instance role with the aforementioned permissions, set up a job queue that points to that compute environment, then create a job definition using the image. You can then manually create a job using that job definition and queue, or set up a EventBridge event to run the job on a schedule. More details on batch can be found in the AWS docs: https://docs.aws.amazon.com/batch/latest/userguide/Batch_GetStarted.html