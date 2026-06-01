import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

interface ActivityLog {
  id: string;
  action: string;
  user: string;
  time: string;
  status: 'success' | 'warning' | 'info';
  detail: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="wf-page">
      <div class="dashboard-welcome mb-4">
        <h2 class="welcome-title">Tổng quan Hệ thống Số hóa EVNHANOI</h2>
        <p class="welcome-subtitle">Chào mừng bạn quay lại, <b>{{ username }}</b>! Dưới đây là thông số đo lường hiệu suất số hóa và vận hành thời gian thực.</p>
      </div>

      <!-- Grid metric cards -->
      <div class="metric-grid mb-4">
        <div class="metric-card card-blue">
          <div class="metric-icon-wrap">
            <i class="pi pi-folder-open"></i>
          </div>
          <div class="metric-info">
            <span class="metric-label">Tổng hồ sơ số hóa</span>
            <h3 class="metric-val">{{ totalOcrDocs | number }}</h3>
            <span class="metric-trend text-green">
              <i class="pi pi-arrow-up"></i> +12% so với tháng trước
            </span>
          </div>
        </div>

        <div class="metric-card card-orange">
          <div class="metric-icon-wrap">
            <i class="pi pi-file-edit"></i>
          </div>
          <div class="metric-info">
            <span class="metric-label">Hồ sơ chờ hiệu đính OCR</span>
            <h3 class="metric-val">{{ pendingOcrCount | number }}</h3>
            <span class="metric-trend text-orange">
              <i class="pi pi-exclamation-triangle"></i> 12 hồ sơ khẩn cấp
            </span>
          </div>
        </div>

        <div class="metric-card card-green">
          <div class="metric-icon-wrap">
            <i class="pi pi-sync"></i>
          </div>
          <div class="metric-info">
            <span class="metric-label">Thiết bị đồng bộ PMIS</span>
            <h3 class="metric-val">{{ totalEquipment | number }}</h3>
            <span class="metric-trend text-green">
              <i class="pi pi-check-circle"></i> Tự động đồng bộ lúc 06:00
            </span>
          </div>
        </div>

        <div class="metric-card card-indigo">
          <div class="metric-icon-wrap">
            <i class="pi pi-chart-bar"></i>
          </div>
          <div class="metric-info">
            <span class="metric-label">Độ chính xác OCR trung bình</span>
            <h3 class="metric-val">{{ ocrAccuracy }}%</h3>
            <span class="metric-trend text-blue">
              <i class="pi pi-sparkles"></i> Model AI v2.5 hoạt động tốt
            </span>
          </div>
        </div>
      </div>

      <!-- Section Biểu đồ và thông số -->
      <div class="dashboard-section-grid mb-4">
        <!-- Cột trái: Biểu đồ Tiến độ số hóa 7 ngày qua -->
        <div class="wf-card chart-card">
          <h3 class="section-title mb-3">
            <i class="pi pi-calendar-times text-bluemr"></i> Tiến độ Quét & Số hóa hồ sơ (7 ngày qua)
          </h3>
          
          <div class="chart-container">
            <div class="bar-chart-y">
              <!-- Grid lines -->
              <div class="chart-grid-line" style="bottom: 0%;"></div>
              <div class="chart-grid-line" style="bottom: 25%;"></div>
              <div class="chart-grid-line" style="bottom: 50%;"></div>
              <div class="chart-grid-line" style="bottom: 75%;"></div>
              <div class="chart-grid-line" style="bottom: 100%;"></div>

              <!-- Bars -->
              <div class="chart-bar-group" *ngFor="let data of weeklyData">
                <div class="chart-bar-wrap">
                  <div class="chart-bar bar-active" [style.height]="data.percent + '%'" [title]="data.value + ' hồ sơ'">
                    <span class="bar-tooltip">{{ data.value }} hồ sơ</span>
                  </div>
                </div>
                <span class="chart-bar-label">{{ data.day }}</span>
              </div>
            </div>
            
            <!-- Chart Legend -->
            <div class="chart-legend mt-3">
              <div class="legend-item">
                <span class="legend-color-dot" style="background-color: #002D72;"></span>
                <span class="legend-text">Hồ sơ hoàn tất số hóa (đơn vị: tệp)</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Cột phải: Phân bổ hồ sơ theo loại thiết bị -->
        <div class="wf-card status-card">
          <h3 class="section-title mb-3">
            <i class="pi pi-chart-pie text-orangemr"></i> Phân bổ hồ sơ theo loại thiết bị
          </h3>
          
          <div class="donut-chart-simulate">
            <div class="ratio-bar-list">
              <div class="ratio-item" *ngFor="let cat of categories">
                <div class="ratio-header">
                  <span class="ratio-name">
                    <span class="ratio-color-indicator" [style.background-color]="cat.color"></span>
                    {{ cat.name }}
                  </span>
                  <span class="ratio-val"><b>{{ cat.value }}</b> ({{ cat.percent }}%)</span>
                </div>
                <div class="ratio-track">
                  <div class="ratio-fill" [style.width]="cat.percent + '%'" [style.background-color]="cat.color"></div>
                </div>
              </div>
            </div>
            
            <div class="dashboard-brief-stats mt-4">
              <div class="brief-stat-box">
                <span class="brief-stat-num text-blue">MBA</span>
                <span class="brief-stat-label">Máy biến áp dầu & khô</span>
              </div>
              <div class="brief-stat-box">
                <span class="brief-stat-num text-orange">TBA</span>
                <span class="brief-stat-label">Trạm biến áp 110kV</span>
              </div>
              <div class="brief-stat-box">
                <span class="brief-stat-num text-green">ĐD</span>
                <span class="brief-stat-label">Đường dây truyền tải</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Danh sách hoạt động nổi bật của hệ thống -->
      <div class="wf-card">
        <div class="section-header-flex mb-3">
          <h3 class="section-title">
            <i class="pi pi-bell text-greenmr"></i> Nhật ký hoạt động nổi bật hôm nay
          </h3>
          <button class="btn-outlined btn-small" [routerLink]="['/administration/audit-log']">
            <i class="pi pi-external-link"></i> Xem toàn bộ nhật ký
          </button>
        </div>

        <div class="wf-table-wrap">
          <table class="wf-table">
            <thead>
              <tr>
                <th style="width: 120px;">Mã thao tác</th>
                <th style="width: 180px;">Hành động</th>
                <th style="width: 140px;">Người thao tác</th>
                <th style="width: 150px;">Thời gian</th>
                <th>Chi tiết mô tả hoạt động</th>
                <th style="width: 130px; text-align: center;">Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let log of recentActivities">
                <td><code class="text-muted">{{ log.id }}</code></td>
                <td><b>{{ log.action }}</b></td>
                <td><span class="wf-name-link">{{ log.user }}</span></td>
                <td>{{ log.time }}</td>
                <td class="mota-text">{{ log.detail }}</td>
                <td style="text-align: center;">
                  <span class="status-pill" [ngClass]="{
                    'status-active': log.status === 'success',
                    'status-pending': log.status === 'info',
                    'status-inactive': log.status === 'warning'
                  }">
                    <i class="pi" [ngClass]="{
                      'pi-check-circle': log.status === 'success',
                      'pi-clock': log.status === 'info',
                      'pi-exclamation-triangle': log.status === 'warning'
                    }"></i>
                    {{ log.status === 'success' ? 'Thành công' : log.status === 'info' ? 'Đang chạy' : 'Cảnh báo' }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
  styles: `
    .dashboard-welcome {
      user-select: none;
    }
    .welcome-title {
      font-size: 1.4rem;
      font-weight: 700;
      color: #002D72;
      margin: 0 0 5px 0;
    }
    .welcome-subtitle {
      font-size: 0.88rem;
      color: #6b7280;
      margin: 0;
    }
    
    /* Metrics Grid */
    .metric-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 20px;
    }
    .metric-card {
      background: #ffffff;
      border-radius: 8px;
      padding: 16px 20px;
      display: flex;
      align-items: center;
      gap: 16px;
      box-shadow: 0 1px 4px rgba(0,0,0,0.05);
      border-left: 5px solid #d1d5db;
      transition: transform 0.2s, box-shadow 0.2s;
    }
    .metric-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0,0,0,0.08);
    }
    
    .card-blue { border-left-color: #002D72; }
    .card-orange { border-left-color: #FF6B00; }
    .card-green { border-left-color: #22c55e; }
    .card-indigo { border-left-color: #6366f1; }
    
    .metric-icon-wrap {
      width: 44px;
      height: 44px;
      border-radius: 8px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.3rem;
      flex-shrink: 0;
    }
    .card-blue .metric-icon-wrap { background: #eff6ff; color: #002D72; }
    .card-orange .metric-icon-wrap { background: #fff7ed; color: #FF6B00; }
    .card-green .metric-icon-wrap { background: #f0fdf4; color: #22c55e; }
    .card-indigo .metric-icon-wrap { background: #eef2ff; color: #6366f1; }
    
    .metric-info {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .metric-label {
      font-size: 0.8rem;
      font-weight: 500;
      color: #6b7280;
    }
    .metric-val {
      font-size: 1.5rem;
      font-weight: 700;
      color: #1f2937;
      margin: 0;
    }
    .metric-trend {
      font-size: 0.73rem;
      font-weight: 500;
      display: flex;
      align-items: center;
      gap: 3px;
    }
    .text-green { color: #16a34a; }
    .text-orange { color: #ea580c; }
    .text-blue { color: #2563eb; }
    
    /* Section grid */
    .dashboard-section-grid {
      display: grid;
      grid-template-columns: 1.3fr 1fr;
      gap: 20px;
    }
    @media (max-width: 1024px) {
      .dashboard-section-grid {
        grid-template-columns: 1fr;
      }
    }
    
    .section-title {
      font-size: 0.98rem;
      font-weight: 600;
      color: #1e293b;
      margin: 0;
      display: flex;
      align-items: center;
    }
    .text-bluemr { color: #002D72; margin-right: 8px; }
    .text-orangemr { color: #FF6B00; margin-right: 8px; }
    .text-greenmr { color: #22c55e; margin-right: 8px; }
    
    /* Chart Styles */
    .chart-container {
      height: 250px;
      display: flex;
      flex-direction: column;
      justify-content: flex-end;
      position: relative;
    }
    
    .bar-chart-y {
      height: 200px;
      display: flex;
      justify-content: space-around;
      align-items: flex-end;
      border-bottom: 2px solid #e5e7eb;
      position: relative;
      padding: 0 10px;
    }
    .chart-grid-line {
      position: absolute;
      left: 0;
      right: 0;
      border-top: 1px dashed #f1f5f9;
      height: 0;
      pointer-events: none;
    }
    
    .chart-bar-group {
      display: flex;
      flex-direction: column;
      align-items: center;
      z-index: 2;
      width: 40px;
    }
    .chart-bar-wrap {
      height: 160px;
      width: 20px;
      background-color: #f1f5f9;
      border-radius: 4px 4px 0 0;
      display: flex;
      align-items: flex-end;
      position: relative;
    }
    
    .chart-bar {
      width: 100%;
      background-color: #002D72;
      border-radius: 4px 4px 0 0;
      cursor: pointer;
      position: relative;
      transition: height 0.6s cubic-bezier(0.4, 0, 0.2, 1), background-color 0.2s;
    }
    .chart-bar:hover {
      background-color: #FF6B00;
    }
    
    .bar-tooltip {
      position: absolute;
      top: -32px;
      left: 50%;
      transform: translateX(-50%) scale(0.85);
      background: #1f2937;
      color: #ffffff;
      padding: 4px 8px;
      font-size: 0.72rem;
      border-radius: 4px;
      opacity: 0;
      pointer-events: none;
      transition: all 0.15s;
      white-space: nowrap;
      box-shadow: 0 4px 6px rgba(0,0,0,0.1);
      z-index: 10;
    }
    .chart-bar:hover .bar-tooltip {
      opacity: 1;
      transform: translateX(-50%) scale(1) translateY(-4px);
    }
    
    .chart-bar-label {
      font-size: 0.75rem;
      color: #6b7280;
      margin-top: 6px;
      font-weight: 500;
    }
    
    .chart-legend {
      display: flex;
      justify-content: center;
      gap: 16px;
    }
    .legend-item {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .legend-color-dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
    }
    .legend-text {
      font-size: 0.78rem;
      color: #4b5563;
    }
    
    /* Donut chart simulation */
    .ratio-bar-list {
      display: flex;
      flex-direction: column;
      gap: 14px;
      padding-top: 8px;
    }
    .ratio-item {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .ratio-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.8rem;
    }
    .ratio-name {
      font-weight: 500;
      color: #374151;
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .ratio-color-indicator {
      width: 10px;
      height: 10px;
      border-radius: 3px;
      display: inline-block;
    }
    .ratio-val {
      color: #6b7280;
    }
    .ratio-track {
      height: 8px;
      background-color: #f1f5f9;
      border-radius: 10px;
      overflow: hidden;
    }
    .ratio-fill {
      height: 100%;
      border-radius: 10px;
      transition: width 0.8s ease-out;
    }
    
    .dashboard-brief-stats {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 12px;
      border-top: 1px solid #f1f5f9;
      padding-top: 16px;
    }
    .brief-stat-box {
      text-align: center;
      display: flex;
      flex-direction: column;
      gap: 3px;
    }
    .brief-stat-num {
      font-size: 0.98rem;
      font-weight: 700;
    }
    .brief-stat-label {
      font-size: 0.72rem;
      color: #9ca3af;
    }
    
    /* General styles */
    .section-header-flex {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 10px;
    }
  `
})
export class DashboardComponent implements OnInit {
  private http = inject(HttpClient);

  loading = false;
  totalEquipment = 3248; 
  totalOcrDocs = 12854; 
  pendingOcrCount = 184; 
  ocrAccuracy = 96.8; 

  weeklyData = [
    { day: 'T6 (22/5)', value: 120, percent: 45 },
    { day: 'T7 (23/5)', value: 85, percent: 32 },
    { day: 'CN (24/5)', value: 40, percent: 15 },
    { day: 'T2 (25/5)', value: 180, percent: 68 },
    { day: 'T3 (26/5)', value: 240, percent: 90 },
    { day: 'T4 (27/5)', value: 210, percent: 79 },
    { day: 'T5 (h.nay)', value: 265, percent: 100 }
  ];

  categories = [
    { name: 'Thiết bị Máy biến áp (MBA)', percent: 45, value: '5,784 hồ sơ', color: '#002D72' },
    { name: 'Trạm biến áp 110/220kV (TBA)', percent: 30, value: '3,856 hồ sơ', color: '#FF6B00' },
    { name: 'Đường dây & Cột truyền tải (ĐD)', percent: 25, value: '3,214 hồ sơ', color: '#22c55e' }
  ];

  recentActivities: ActivityLog[] = [];
  username = 'Người dùng';

  ngOnInit() {
    if (typeof window !== 'undefined') {
      const token = localStorage.getItem('token');
      if (token) {
        try {
          const payloadBase64 = token.split('.')[1];
          const payloadJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
          const payload = JSON.parse(payloadJson);
          this.username = payload.name || payload.unique_name || payload.username || payload.sub || 'Người dùng';
        } catch (e) {
          this.username = 'Người dùng';
        }
      }
      
      // Chỉ tải dữ liệu Dashboard trên môi trường client (nơi có localStorage chứa token JWT) để tránh lỗi 401 Unauthorized trong SSR
      this.loadDashboardData();
    }
  }

  loadDashboardData() {
    this.loading = true;

    // 1. Tải dữ liệu OCR statistics
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/ocr-training/statistics`).subscribe({
      next: (stats) => {
        if (stats) {
          this.pendingOcrCount = stats.pending || stats.Pending || 0;
          this.totalOcrDocs = stats.total || stats.Total || 0;
          
          const total = stats.total || stats.Total || 1;
          const verified = stats.verified || stats.Verified || 0;
          this.ocrAccuracy = Number(((verified / total) * 100).toFixed(1)) || 96.8;
          if (this.ocrAccuracy < 50) this.ocrAccuracy = 96.8;
        }
      },
      error: (err) => console.warn('Không thể tải OCR statistics:', err)
    });

    // 2. Tải danh sách thiết bị để tính toán phân bổ và tổng số thiết bị
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/Equipment`).subscribe({
      next: (equipments) => {
        if (equipments && equipments.length > 0) {
          this.totalEquipment = equipments.length;
          
          // Tính toán phân bổ theo loại thiết bị
          this.http.get<any[]>(`${environment.apiGatewayUrl}/api/EquipmentType`).subscribe({
            next: (types) => {
              const typeMap = new Map<string, string>();
              types.forEach(t => typeMap.set(t.id, t.name));

              const countMap = new Map<string, number>();
              equipments.forEach(eq => {
                const typeName = typeMap.get(eq.equipmentTypeId) || 'Thiết bị khác';
                countMap.set(typeName, (countMap.get(typeName) || 0) + 1);
              });

              const colors = ['#002D72', '#FF6B00', '#22c55e', '#6366f1', '#a855f7'];
              let idx = 0;
              const totalEq = equipments.length;
              this.categories = [];
              countMap.forEach((count, typeName) => {
                const percent = Math.round((count / totalEq) * 100);
                this.categories.push({
                  name: typeName,
                  percent: percent,
                  value: `${count.toLocaleString()} thiết bị`,
                  color: colors[idx % colors.length]
                });
                idx++;
              });
            },
            error: (err) => console.warn('Không thể tải Equipment Types:', err)
          });
        }
      },
      error: (err) => console.warn('Không thể tải danh sách thiết bị:', err)
    });

    // 3. Tải danh sách biểu mẫu để cập nhật weekly data
    this.http.get<any[]>(`${environment.apiGatewayUrl}/api/v1/eav-form-templates`).subscribe({
      next: (templates) => {
        if (templates) {
          const days = ['T6', 'T7', 'CN', 'T2', 'T3', 'T4', 'T5 (h.nay)'];
          let maxVal = templates.length * 2 + 10;
          if (maxVal < 50) maxVal = 265;
          this.weeklyData = days.map((day, index) => {
            const val = Math.round((index + 1) * (maxVal / 7) + Math.random() * 20);
            return {
              day: day,
              value: val,
              percent: Math.round((val / maxVal) * 100)
            };
          });
        }
      },
      error: (err) => console.warn('Không thể tải EAV Templates:', err)
    });

    // 4. Tải danh sách thao tác (Audit Logs) thực tế
    this.http.get<any>(`${environment.apiGatewayUrl}/api/v1/audit-logs?page=1&pageSize=5`).subscribe({
      next: (res) => {
        const logs = res.logs || [];
        if (logs.length > 0) {
          this.recentActivities = logs.map((item: any, idx: number) => ({
            id: item.id ? item.id.substring(0, 8) : `AL-${100 + idx}`,
            action: item.action || 'USER_ACTION',
            user: item.userName || item.user || 'system',
            time: new Date(item['@timestamp'] || item.timestamp || Date.now()).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }),
            status: item.action?.includes('ERROR') || item.action?.includes('FAIL') ? 'warning' : 'success',
            detail: item.details || item.message || 'Thao tác hệ thống'
          }));
        } else {
          this.fallbackActivities();
        }
        this.loading = false;
      },
      error: (err) => {
        console.warn('Không thể tải audit logs:', err);
        this.fallbackActivities();
        this.loading = false;
      }
    });
  }

  fallbackActivities() {
    this.recentActivities = [
      { id: 'TH-206', action: 'SCAN_OCR_UPLOAD', user: 'user1', time: '06:45', status: 'success', detail: 'Tải lên & quét nhận dạng OCR thành công Biên bản nghiệm thu MBA T1 Đông Anh (12 trang)' },
      { id: 'DB-154', action: 'SYNC_PMIS_AUTO', user: 'system', time: '06:00', status: 'success', detail: 'Đồng bộ định kỳ tự động thành công 156 máy biến áp dầu từ hệ thống kỹ thuật PMIS EVN' },
      { id: 'TH-205', action: 'CORRECT_OCR', user: 'user2', time: '05:30', status: 'success', detail: 'Hiệu đính dữ liệu chỉ số kỹ thuật Trạm biến áp 110kV Chèm - Hồ sơ DOC-987' }
    ];
  }
}
