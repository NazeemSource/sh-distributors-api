$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$api = Start-Process -FilePath dotnet -ArgumentList @('run','--no-build','--configuration','Release','--launch-profile','http') -WorkingDirectory $project -WindowStyle Hidden -PassThru
try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try { Invoke-RestMethod 'http://127.0.0.1:5218/health' | Out-Null; $ready = $true; break } catch { Start-Sleep -Milliseconds 500 }
    }
    if (!$ready) { throw 'API did not become healthy.' }
    $login = Invoke-RestMethod 'http://127.0.0.1:5218/api/auth/login' -Method Post -ContentType 'application/json' -Body (@{username='admin';password='1234'}|ConvertTo-Json)
    $headers = @{Authorization="Bearer $($login.accessToken)"}
    $suffix = [Guid]::NewGuid().ToString('N').Substring(0,8)
    $company = Invoke-RestMethod 'http://127.0.0.1:5218/api/companies' -Method Post -Headers $headers -ContentType 'application/json' -Body (@{code="COMPANY-$suffix";name="Company - $suffix";contactName='';phone='';address='';active=$true}|ConvertTo-Json)
    $shop = Invoke-RestMethod 'http://127.0.0.1:5218/api/shops' -Method Post -Headers $headers -ContentType 'application/json' -Body (@{companyId=$company.id;code="CUSTOMER-$suffix";name="Customer - $suffix";contactName='';phone='';address='Address - 01';city='City - 01';creditLimit=10000;active=$true}|ConvertTo-Json)
    $product = Invoke-RestMethod 'http://127.0.0.1:5218/api/products' -Method Post -Headers $headers -ContentType 'application/json' -Body (@{companyId=$company.id;sku="SKU-$suffix";barcode="BC-$suffix";name="Product - $suffix";category='General';sellingPrice=123.00;costPrice=80.00;reorderLevel=5;expiryDate=$null;openingStock=100;active=$true}|ConvertTo-Json)
    $rep = Invoke-RestMethod 'http://127.0.0.1:5218/api/users' -Method Post -Headers $headers -ContentType 'application/json' -Body (@{companyId=$company.id;name="Rep - $suffix";username="rep$suffix";password='TestPassword123!';role='Rep';territory='Area - 01';active=$true}|ConvertTo-Json)
    $today = Get-Date -Format 'yyyy-MM-dd'; $delivery = (Get-Date).AddDays(1).ToString('yyyy-MM-dd')
    $order = Invoke-RestMethod 'http://127.0.0.1:5218/api/orders' -Method Post -Headers $headers -ContentType 'application/json' -Body (@{shopId=$shop.id;salesRepId=$rep.id;orderNumber="ORD-$suffix";orderDate=$today;deliveryDate=$delivery;deliveryAddress='Address - 01';notes='';products=@(@{productId=$product.id;quantity=2;freeIssueQuantity=1;unitPrice=123.00})}|ConvertTo-Json -Depth 5)
    Invoke-RestMethod "http://127.0.0.1:5218/api/orders/$($order.id)/payments" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{paymentDate=$today;paidAmount=100;method='CASH';reference="PAY-$suffix"}|ConvertTo-Json) | Out-Null
    $cheque = Invoke-RestMethod 'http://127.0.0.1:5218/api/cheques' -Method Post -Headers $headers -ContentType 'application/json' -Body (@{shopId=$shop.id;orderId=$order.id;chequeNumber="CHQ-$suffix";bankName='Bank - 01';amount=146;chequeDate=$delivery;status='PENDING';remindBeforeDays=3;notes=''}|ConvertTo-Json)
    $upcoming = Invoke-RestMethod 'http://127.0.0.1:5218/api/cheques/upcoming' -Headers $headers
    $stock = Invoke-RestMethod "http://127.0.0.1:5218/api/products/$($product.id)/stock" -Headers $headers
    [pscustomobject]@{Health='OK';OrderTotal=$order.orderTotal;CurrentStock=$stock.currentStock;ChequeId=$cheque.id;UpcomingCheques=@($upcoming).Count}
}
finally {
    if (!$api.HasExited) { Stop-Process -Id $api.Id -Force }
}
