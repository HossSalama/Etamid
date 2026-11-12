import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminLanguageService } from '../../core/services/admin-language.service';

interface NavItem {
  labelKey: string;
  path: string;
  icon: string;
  permission?: string;
  superAdminOnly?: boolean;
}

@Component({
  selector: 'app-admin-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './admin-shell.component.html',
  styleUrl: './admin-shell.component.scss',
})
export class AdminShellComponent {
  protected readonly auth = inject(AdminAuthService);
  protected readonly lang = inject(AdminLanguageService);
  private readonly router = inject(Router);

  protected sidebarOpen = signal(false);

  protected readonly admin = this.auth.currentAdmin;

  protected readonly adminInitials = computed(() => {
    const a = this.admin();
    if (!a) return 'A';
    return a.name
      .split(' ')
      .map((p) => p[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  });

  protected readonly navGroups: { labelKey: string; items: NavItem[] }[] = [
    {
      labelKey: 'nav_dashboard',
      items: [
        { labelKey: 'nav_dashboard', path: '/dashboard', icon: '⊞' },
      ],
    },
    {
      labelKey: 'nav_operators',
      items: [
        { labelKey: 'nav_withdrawals', path: '/withdrawals',  icon: '⇥', permission: 'admin.withdrawals.view' },
        { labelKey: 'nav_penalties',   path: '/penalties',    icon: '⚠', permission: 'admin.penalties.view' },
        { labelKey: 'nav_disputes',    path: '/disputes',     icon: '⚖', permission: 'admin.disputes.view' },
        { labelKey: 'nav_deals',       path: '/deals',        icon: '🤝', permission: 'admin.deals.view' },
      ],
    },
    {
      labelKey: 'nav_management',
      items: [
        { labelKey: 'nav_admins',      path: '/admins',       icon: '👤', permission: 'admin.roles.manage' },
        { labelKey: 'nav_roles',       path: '/roles',        icon: '🔑', permission: 'admin.roles.manage' },
        { labelKey: 'nav_audit_log',   path: '/audit-log',    icon: '📋', permission: 'admin.audit.view' },
      ],
    },
    {
      labelKey: 'nav_profile',
      items: [
        { labelKey: 'nav_profile', path: '/profile', icon: '⚙' },
      ],
    },
  ];

  protected isItemVisible(item: NavItem): boolean {
    if (!item.permission) return true;
    return this.auth.hasPermission(item.permission);
  }

  protected visibleGroupItems(group: { items: NavItem[] }): NavItem[] {
    return group.items.filter((i) => this.isItemVisible(i));
  }

  protected toggleSidebar(): void {
    this.sidebarOpen.update((v) => !v);
  }

  protected closeSidebar(): void {
    this.sidebarOpen.set(false);
  }

  protected logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }

  protected logoutAll(): void {
    this.auth.logoutAll().subscribe({
      complete: () => this.router.navigate(['/login']),
      error:    () => this.router.navigate(['/login']),
    });
  }

  protected toggleLang(): void {
    this.lang.toggle();
  }
}
