import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { map } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

function withPermissionsLoaded(
  resolver: (auth: AuthService, router: Router) => boolean | UrlTree
): CanActivateFn {
  return () => {
    const router = inject(Router);
    const auth = inject(AuthService);

    return auth.ensurePermissionsLoaded().pipe(
      map(() => resolver(auth, router))
    );
  };
}

function deny(router: Router): UrlTree {
  return router.createUrlTree(['/error'], { queryParams: { code: '403' } });
}

/** Menu Cấu hình kết nối API PMIS (endpoint + header) */
export const pmisEndpointConfigMenuGuard = withPermissionsLoaded((auth, router) => {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('PMIS_ENDPOINT_CONFIG_VIEW') ||
    auth.hasPermission('PMIS_ENDPOINT_CONFIG_EDIT')
  ) {
    return true;
  }
  return deny(router);
});

/** Menu Đồng bộ thủ công PMIS (Trạm/Đường dây/Thiết bị) */
export const pmisManualSyncMenuGuard = withPermissionsLoaded((auth, router) => {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('PMIS_MANUAL_SYNC_VIEW') ||
    auth.hasPermission('PMIS_MANUAL_SYNC_CREATE')
  ) {
    return true;
  }
  return deny(router);
});

/**
 * Menu Lịch đồng bộ PMIS (bật/tắt, tần suất, lịch sử).
 * Mã quyền do PermissionCodeResolver tự suy ra từ tên controller SyncScheduleController
 * (ToSnakeCase("SyncSchedule") = "SYNC_SCHEDULE") — không cấu hình tay.
 */
export const pmisScheduleMenuGuard = withPermissionsLoaded((auth, router) => {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('SYNC_SCHEDULE_VIEW') ||
    auth.hasPermission('SYNC_SCHEDULE_EDIT')
  ) {
    return true;
  }
  return deny(router);
});

/**
 * Menu Ánh xạ mã đơn vị PMIS (PMIS_UNIT_CODE_MAPPING).
 * Mã quyền PHẢI khớp đúng mã do PermissionCodeResolver tự suy ra từ tên controller
 * PmisUnitCodeMappingController (ToSnakeCase("PmisUnitCodeMapping") = "PMIS_UNIT_CODE_MAPPING") —
 * sai 1 ký tự (vd thiếu "CODE") sẽ khiến menu vẫn hiện đúng (do check ở đây) nhưng gọi API thật lại
 * bị 403 vì DynamicPermissionFilter đòi mã khác, chỉ không lộ ra khi test bằng tài khoản ADMIN (bypass
 * toàn bộ kiểm tra quyền).
 */
export const pmisUnitMappingMenuGuard = withPermissionsLoaded((auth, router) => {
  if (
    auth.hasPermission('SUPER_ADMIN') ||
    auth.hasPermission('PMIS_UNIT_CODE_MAPPING_VIEW') ||
    auth.hasPermission('PMIS_UNIT_CODE_MAPPING_CREATE')
  ) {
    return true;
  }
  return deny(router);
});
