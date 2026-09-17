// PayFlow Enterprise Frontend State & Orchestration
const state = {
  token: '',
  role: 'Customer',
  email: 'customer@payflow.com',
  products: [],
  orders: [],
  payments: {},
  pollTimer: null
};

// DOM Yüklendiğinde Başlat
document.addEventListener('DOMContentLoaded', async () => {
  await switchPersona('Customer');
  await loadCatalogProducts();
  await loadRecentOrders();
  checkGatewayHealth();

  // Her 8 saniyede bir siparişleri ve ödeme durumlarını otomatik güncelle
  state.pollTimer = setInterval(loadRecentOrders, 8000);
});

// 1. JWT Kimlik Değiştirici
async function switchPersona(role) {
  state.role = role;
  state.email = role === 'Admin' ? 'admin@payflow.com' : 'customer@payflow.com';

  const btnCust = document.getElementById('btn-login-customer');
  const btnAdmin = document.getElementById('btn-login-admin');
  const roleTag = document.getElementById('auth-status-tag');
  const emailInput = document.getElementById('order-email');

  if (role === 'Admin') {
    btnAdmin.classList.add('active');
    btnCust.classList.remove('active');
    roleTag.textContent = 'Admin Role';
    roleTag.className = 'auth-role-tag role-admin';
  } else {
    btnCust.classList.add('active');
    btnAdmin.classList.remove('active');
    roleTag.textContent = 'Customer Role';
    roleTag.className = 'auth-role-tag role-customer';
  }

  if (emailInput) emailInput.value = state.email;

  try {
    const res = await fetch('/api/auth/login', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Correlation-ID': `web-auth-${Date.now()}`
      },
      body: JSON.stringify({
        email: state.email,
        password: 'Password123!'
      })
    });

    if (!res.ok) throw new Error('Kimlik doğrulanamadı');

    const data = await res.json();
    state.token = data.token;

    const tokenBox = document.getElementById('jwt-token-display');
    if (tokenBox) {
      tokenBox.textContent = `Bearer ${state.token.substring(0, 36)}...[${state.token.length} chars]`;
      tokenBox.title = state.token;
    }

    showToast(`Giriş başarılı: ${state.email} (${role})`, 'success');
  } catch (err) {
    console.error('Login error:', err);
    showToast('JWT Giriş hatası oluştu', 'error');
  }
}

// 2. Katalog & Redis Cache-Aside
async function loadCatalogProducts() {
  const startTime = performance.now();
  const badge = document.getElementById('cache-source-badge');
  const latencyText = document.getElementById('cache-latency-text');
  const container = document.getElementById('products-grid');
  const selectBox = document.getElementById('order-product-select');

  try {
    const res = await fetch('/api/catalog', {
      headers: {
        'X-Correlation-ID': `web-catalog-${Date.now()}`
      }
    });

    const elapsed = Math.round(performance.now() - startTime);
    latencyText.textContent = `${elapsed} ms`;

    if (!res.ok) throw new Error(`Katalog çekilemedi (HTTP ${res.status})`);
    const result = await res.json();

    // API { source: "...", count: N, data: [...] } döner
    const products = Array.isArray(result) ? result : (result.data || []);
    const source = result.source || (elapsed < 30 ? 'Redis Cache' : 'Database (EF Core)');
    state.products = products;

    if (source.includes('Redis')) {
      badge.textContent = `⚡ ${source} (${elapsed} ms)`;
      badge.className = 'cache-source-badge source-redis';
    } else {
      badge.textContent = `🐘 ${source} (${elapsed} ms)`;
      badge.className = 'cache-source-badge source-db';
    }

    // Ürün Kartlarını Çiz
    container.innerHTML = '';
    selectBox.innerHTML = '';

    if (products.length === 0) {
      container.innerHTML = `<p style="color: var(--text-muted); font-size: 0.8rem;">Katalogda henüz ürün bulunmuyor.</p>`;
      return;
    }

    products.forEach(p => {
      // Kart
      const card = document.createElement('div');
      card.className = 'product-card';
      card.innerHTML = `
        <div>
          <div class="product-name">${escapeHtml(p.name)}</div>
          <div class="product-desc">${escapeHtml(p.description || '')}</div>
        </div>
        <div class="product-footer">
          <div class="product-price">${formatCurrency(p.price)}</div>
          <button class="btn-select-product" onclick="selectProduct('${p.id}')">Seç</button>
        </div>
      `;
      container.appendChild(card);

      // Select Option
      const opt = document.createElement('option');
      opt.value = p.id;
      opt.textContent = `${p.name} - ${formatCurrency(p.price)}`;
      selectBox.appendChild(opt);
    });

    updateOrderTotal();
  } catch (err) {
    console.error('Catalog fetch error:', err);
    container.innerHTML = `<p style="color: var(--accent-rose); font-size: 0.8rem;">Katalog çekilemedi: ${escapeHtml(err.message)}</p>`;
  }
}

// Redis Önbellek Temizleme (Cache Invalidation Testi)
async function purgeRedisCache() {
  const badge = document.getElementById('cache-source-badge');
  badge.textContent = '🔄 Önbellek boşaltılıyor...';
  badge.className = 'cache-source-badge source-db';

  try {
    const res = await fetch('/api/catalog/cache/purge', {
      method: 'POST',
      headers: {
        'X-Correlation-ID': `web-purge-${Date.now()}`
      }
    });

    if (res.ok) {
      showToast('Redis önbelleği temizlendi! Bir sonraki sorgu doğrudan PostgreSQL\'den gelecek.', 'success');
    }
  } catch (e) {
    console.warn('Purge error:', e);
  }

  setTimeout(loadCatalogProducts, 400);
}

function selectProduct(productId) {
  const selectBox = document.getElementById('order-product-select');
  if (selectBox) {
    selectBox.value = productId;
    updateOrderTotal();
    showToast('Ürün sepete eklendi!', 'success');
  }
}

function updateOrderTotal() {
  const selectBox = document.getElementById('order-product-select');
  const qtyInput = document.getElementById('order-quantity');
  const totalDisplay = document.getElementById('order-total-display');

  if (!selectBox || !selectBox.value || state.products.length === 0) return;

  const product = state.products.find(p => p.id === selectBox.value);
  const qty = parseInt(qtyInput.value, 10) || 1;

  if (product) {
    const total = product.price * qty;
    totalDisplay.textContent = formatCurrency(total);
  }
}

// 3. Sipariş Verme (CQRS Command & Transactional Outbox)
async function handleCreateOrder(e) {
  e.preventDefault();

  if (!state.token) {
    showToast('Lütfen önce JWT ile giriş yapın.', 'error');
    return;
  }

  const selectBox = document.getElementById('order-product-select');
  const qtyInput = document.getElementById('order-quantity');
  const cityInput = document.getElementById('order-city');
  const btn = document.getElementById('btn-submit-order');

  const product = state.products.find(p => p.id === selectBox.value);
  if (!product) {
    showToast('Lütfen listeden bir ürün seçin.', 'error');
    return;
  }

  const qty = parseInt(qtyInput.value, 10) || 1;
  const correlationId = `web-order-${Date.now()}`;

  const payload = {
    customerId: "22222222-2222-2222-2222-222222222222",
    customerEmail: state.email,
    street: "Büyükdere Cad. No: 199",
    city: cityInput.value || "İstanbul",
    country: "Türkiye",
    zipCode: "34394",
    items: [
      {
        productId: product.id,
        productName: product.name,
        unitPrice: product.price,
        quantity: qty
      }
    ]
  };

  try {
    btn.disabled = true;
    btn.innerHTML = '<span>⏳ İşleniyor...</span>';

    // 1. Adımı vurgula
    highlightPipelineStep(1);

    const res = await fetch('/api/orders', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${state.token}`,
        'X-Correlation-ID': correlationId
      },
      body: JSON.stringify(payload)
    });

    if (res.status === 401) {
      throw new Error('401 Yetkisiz İstek. JWT Token geçersiz veya eksik!');
    }

    if (!res.ok) {
      const errData = await res.json().catch(() => ({}));
      throw new Error(errData.message || `Sipariş oluşturulamadı (${res.status})`);
    }

    const data = await res.json();
    showToast(`Sipariş #${data.orderId.substring(0, 8)} oluşturuldu! Outbox ve RabbitMQ tetiklendi.`, 'success');

    // Pipeline adımlarını görselleştir
    setTimeout(() => highlightPipelineStep(2), 1000);
    setTimeout(() => highlightPipelineStep(3), 2000);
    setTimeout(() => highlightPipelineStep(4), 3000);
    setTimeout(() => {
      highlightPipelineStep(5);
      loadRecentOrders();
    }, 4500);

  } catch (err) {
    console.error('Create order error:', err);
    showToast(err.message, 'error');
  } finally {
    btn.disabled = false;
    btn.innerHTML = '<span>⚡ Siparişi Tamamla & Outbox\'a Yaz</span>';
  }
}

// 4. Siparişleri ve Ödeme Kayıtlarını Listeleme
async function loadRecentOrders() {
  const tbody = document.getElementById('orders-table-body');
  if (!tbody) return;

  try {
    // 1. Siparişleri Çek
    const orderHeaders = {
      'X-Correlation-ID': `web-getorders-${Date.now()}`
    };
    if (state.token) {
      orderHeaders['Authorization'] = `Bearer ${state.token}`;
    }

    const ordersRes = await fetch('/api/orders', { headers: orderHeaders });
    if (!ordersRes.ok) return;
    const ordersData = await ordersRes.json();
    const orders = Array.isArray(ordersData) ? ordersData : (ordersData.value || ordersData.data || []);

    // 2. Ödeme Kayıtlarını Çek (Banka SOAP kodları için)
    const payRes = await fetch('/api/payments').catch(() => null);
    let paymentsMap = {};
    if (payRes && payRes.ok) {
      const payments = await payRes.json();
      const list = Array.isArray(payments) ? payments : (payments.data || []);
      list.forEach(p => {
        paymentsMap[p.orderId] = p;
      });
    }

    if (orders.length === 0) {
      tbody.innerHTML = `<tr><td colspan="6" style="text-align: center; color: var(--text-muted); padding: 1.5rem;">Henüz kayıtlı sipariş bulunmuyor. Sol panelden yeni sipariş verin!</td></tr>`;
      return;
    }

    tbody.innerHTML = '';
    orders.forEach(ord => {
      const payment = paymentsMap[ord.id];
      const isPaid = ord.status === 'Paid' || (payment && payment.status === 'Approved');
      const txnCode = payment?.bankTransactionCode || (isPaid ? 'SOAP_APPROVED' : 'Kuyrukta');

      const tr = document.createElement('tr');
      tr.innerHTML = `
        <td style="font-family: monospace; font-weight: 700; color: var(--accent-cyan);">
          #${ord.id.substring(0, 8)}...
        </td>
        <td>${escapeHtml(ord.customerEmail)}</td>
        <td style="font-weight: 700;">${formatCurrency(ord.totalAmount)}</td>
        <td>
          <span class="order-badge ${isPaid ? 'badge-paid' : 'badge-submitted'}">
            ${isPaid ? '✓ Ödendi (Paid)' : '⏳ İşleniyor (Submitted)'}
          </span>
        </td>
        <td style="font-family: monospace; font-size: 0.75rem; color: var(--text-secondary);">
          ${txnCode}
        </td>
        <td>
          <button class="btn-secondary" style="padding: 0.25rem 0.6rem; font-size: 0.7rem;" onclick="checkOrderDetail('${ord.id}')">
            Detay
          </button>
        </td>
      `;
      tbody.appendChild(tr);
    });

  } catch (err) {
    console.error('Load orders error:', err);
  }
}

async function checkOrderDetail(orderId) {
  try {
    const res = await fetch(`/api/orders/${orderId}`, {
      headers: {
        'Authorization': `Bearer ${state.token}`
      }
    });
    if (!res.ok) throw new Error('Sipariş detayı çekilemedi');
    const order = await res.json();

    alert(`Sipariş Detayı:\nID: ${order.id}\nDurum: ${order.status}\nMüşteri: ${order.customerEmail}\nTutar: ${order.totalAmount} TRY\nAdres: ${order.street}, ${order.city}`);
  } catch (err) {
    showToast(err.message, 'error');
  }
}

// 5. Pipeline Adımını Vurgula
function highlightPipelineStep(stepNumber) {
  for (let i = 1; i <= 5; i++) {
    const el = document.getElementById(`step-${i}`);
    if (!el) continue;
    if (i < stepNumber) {
      el.className = 'pipeline-step done';
    } else if (i === stepNumber) {
      el.className = 'pipeline-step active';
    } else {
      el.className = 'pipeline-step';
    }
  }
}

// 6. Sistem Sağlık Kontrolü
async function checkGatewayHealth() {
  try {
    const res = await fetch('/health');
    if (res.ok) {
      const text = document.getElementById('gateway-status-text');
      if (text) text.textContent = 'Tüm Mikroservisler Aktif (Gateway :5000)';
    }
  } catch (e) {
    // Gateway başlatılıyor olabilir
  }
}

// 7. Bildirim (Toast) Yardımcısı
function showToast(message, type = 'success') {
  const container = document.getElementById('toast-container');
  if (!container) return;

  const toast = document.createElement('div');
  toast.className = `toast ${type}`;
  toast.innerHTML = `
    <span>${type === 'success' ? '✅' : '⚠️'}</span>
    <div>${escapeHtml(message)}</div>
  `;

  container.appendChild(toast);
  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateX(100%)';
    toast.style.transition = 'all 0.3s ease';
    setTimeout(() => toast.remove(), 300);
  }, 4000);
}

// Yardımcı Formatlayıcılar
function formatCurrency(val) {
  return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(val);
}

function escapeHtml(str) {
  if (!str) return '';
  return str.replace(/[&<>"']/g, m => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#039;'
  })[m]);
}
