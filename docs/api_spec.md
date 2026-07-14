# REST API Specification

This document details the REST API specifications for the AI-Powered Business ERP system.

* **Base URL**: `https://api.erp.domain.com/v1`
* **Default Headers**:
  * `Content-Type: application/json`
  * `Authorization: Bearer <JWT_Token>`
* **Status Code Mappings**:
  * `200 OK`: Successful synchronous execution.
  * `201 Created`: Resource successfully created.
  * `202 Accepted`: DML operation parsed; awaiting approval token confirmation.
  * `400 Bad Request`: Validation failure or execution syntax blocking error.
  * `401 Unauthorized`: Missing or expired JWT.
  * `403 Forbidden`: Insufficient role permissions.
  * `500 Internal Error`: DB exception or downstream provider crash.

---

## 1. Chat & NLP Endpoints

### POST /chat
Submits a natural language query or transaction statement.

* **Payload**:
```json
{
  "prompt": "Show today's orders"
}
```

* **Response (Read-Only SELECT Result)**:
  * *Status Code*: `200 OK`
```json
{
  "executionMode": "Synchronous",
  "intent": "RecentOrders",
  "explanation": "Retrieving list of orders created on 2026-07-14.",
  "sqlQuery": "SELECT OrderID, OrderNumber, CustomerID, OrderDate, TotalAmount, OrderStatus FROM dbo.Orders WHERE CAST(OrderDate AS DATE) = CAST(SYSUTCDATETIME() AS DATE) AND IsDeleted = 0;",
  "data": [
    {
      "orderID": 1025,
      "orderNumber": "ORD-2026-004",
      "customerID": 12,
      "orderDate": "2026-07-14T08:30:00Z",
      "totalAmount": 150000.00,
      "orderStatus": "Pending"
    }
  ]
}
```

* **Response (Write/DML - Requires Confirmation)**:
  * *Status Code*: `202 Accepted`
```json
{
  "executionMode": "AwaitingApproval",
  "intent": "CreateOrder",
  "explanation": "This will add a new order for customer XYZ Traders with 1 item of Product ID 5 (Rate: 10,000, Qty: 2) totaling ₹20,000.",
  "approvalToken": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "generatedSql": "INSERT INTO dbo.Orders (OrderNumber, CustomerID, OrderDate, OrderStatus, TotalAmount, PaidAmount) VALUES ('ORD-2026-005', 12, SYSUTCDATETIME(), 'Pending', 20000.00, 0.00);\nINSERT INTO dbo.OrderItems (OrderID, ProductID, Description, Quantity, Unit, Rate) VALUES (SCOPE_IDENTITY(), 5, 'Product Detail', 2.0000, 'pcs', 10000.00);"
}
```

---

### POST /voice
Submits binary voice audio to be transcribed and executed.

* **Content-Type**: `multipart/form-data`
* **Request Parameters**:
  * `audio`: Binary file (WAV, MP3, WebM, M4A; Max 10MB)
* **Response**: Identical structure to `POST /chat` (includes transcription string under `transcription`).
```json
{
  "transcription": "add a new order for XYZ Traders",
  "executionMode": "AwaitingApproval",
  "intent": "CreateOrder",
  "explanation": "Drafting an order for XYZ Traders. Please supply missing parameters: product, quantity, rate.",
  "approvalToken": null,
  "data": null
}
```

---

### POST /approve
Confirms execution of a cached draft operation using the approval token.

* **Payload**:
```json
{
  "approvalToken": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d"
}
```

* **Response**:
  * *Status Code*: `200 OK`
```json
{
  "status": "Success",
  "message": "SQL statement successfully executed.",
  "rowsAffected": 2,
  "logId": 45098
}
```

---

### POST /reject
Rejects and deletes a cached draft operation, logging the cancellation.

* **Payload**:
```json
{
  "approvalToken": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "reason": "Incorrect quantity specified by user"
}
```

* **Response**:
  * *Status Code*: `200 OK`
```json
{
  "status": "Cancelled",
  "message": "Operation successfully aborted.",
  "logId": 45099
}
```

---

## 2. Standard Query & Ledger Endpoints

### GET /customers
Lists customers with optional filters.
* **Query Params**: `search=XYZ`, `page=1`, `pageSize=20`
* **Response**: `200 OK`
```json
{
  "items": [
    {
      "customerId": 12,
      "customerCode": "XYZ001",
      "companyName": "XYZ Traders",
      "contactPerson": "Rajesh Kumar",
      "phone": "+919876543210",
      "createdDate": "2026-01-10T12:00:00Z"
    }
  ],
  "totalCount": 1
}
```

---

### GET /orders
Retrieves order history.
* **Query Params**: `customerId=12`, `status=Pending`, `page=1`
* **Response**: `200 OK`
```json
{
  "orders": [
    {
      "orderId": 1025,
      "orderNumber": "ORD-2026-004",
      "orderDate": "2026-07-14T08:30:00Z",
      "deliveryDate": "2026-07-20T00:00:00Z",
      "orderStatus": "Pending",
      "totalAmount": 150000.00,
      "paidAmount": 50000.00,
      "pendingAmount": 100000.00,
      "remarks": "Urgent delivery"
    }
  ]
}
```

---

### GET /payments
Retrieves payment transaction logs.
* **Query Params**: `customerId=12`, `startDate=2026-07-01`
* **Response**: `200 OK`
```json
{
  "payments": [
    {
      "paymentId": 503,
      "customerId": 12,
      "orderId": 1025,
      "paymentDate": "2026-07-14T10:00:00Z",
      "amount": 50000.00,
      "paymentMode": "UPI",
      "referenceNumber": "UPI4892019482",
      "remarks": "Partial payment"
    }
  ]
}
```

---

### GET /ledger
Generates a structured debit/credit ledger card statement for a specific customer.
* **Query Params**: `customerId=12`, `startDate=2026-01-01`, `endDate=2026-07-14`
* **Response**: `200 OK`
```json
{
  "customerId": 12,
  "companyName": "XYZ Traders",
  "openingBalance": 0.00,
  "closingBalance": 100000.00,
  "transactions": [
    {
      "date": "2026-07-14T08:30:00Z",
      "type": "Invoice",
      "reference": "ORD-2026-004",
      "debit": 150000.00,
      "credit": 0.00,
      "runningBalance": 150000.00
    },
    {
      "date": "2026-07-14T10:00:00Z",
      "type": "Receipt",
      "reference": "UPI4892019482",
      "debit": 0.00,
      "credit": 50000.00,
      "runningBalance": 100000.00
    }
  ]
}
```

---

### GET /reports
Pulls high-level summary report details.
* **Query Params**: `type=monthly_sales`, `year=2026`
* **Response**: `200 OK`
```json
{
  "reportType": "monthly_sales",
  "year": 2026,
  "currency": "INR",
  "dataPoints": [
    { "label": "January", "value": 1200000.00 },
    { "label": "February", "value": 980000.00 },
    { "label": "March", "value": 1500000.00 },
    { "label": "July", "value": 340000.00 }
  ]
}
```
