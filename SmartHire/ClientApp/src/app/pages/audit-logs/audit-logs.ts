import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuditLog, AuditLogsService } from '../../core/services/audit-logs.service';

@Component({
  selector: 'app-audit-logs',
  imports: [CommonModule],
  templateUrl: './audit-logs.html',
  styleUrl: './audit-logs.scss',
})
export class AuditLogs implements OnInit {
  readonly logs = signal<AuditLog[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  constructor(private readonly auditLogsService: AuditLogsService) {}

  ngOnInit(): void {
    this.auditLogsService.getAll().subscribe({
      next: (logs) => {
        this.logs.set(logs);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load audit logs.');
        this.loading.set(false);
      },
    });
  }
}
