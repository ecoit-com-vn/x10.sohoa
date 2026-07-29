import { RenderMode, ServerRoute } from '@angular/ssr';

/** Hash routing: fragment không gửi lên server — chỉ render phía client để giữ URL sau reload. */
export const serverRoutes: ServerRoute[] = [
  {
    path: '**',
    renderMode: RenderMode.Client,
  },
];
