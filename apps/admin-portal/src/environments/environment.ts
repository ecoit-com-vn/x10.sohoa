export const environment = {
  production: false,
  // URL của API Gateway (YARP) - chạy local port 5000
  apiGatewayUrl: 'http://localhost:5000',
  // URL trực tiếp WorkflowService nếu bypass gateway
  workflowServiceUrl: 'http://localhost:5007',
};
