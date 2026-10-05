import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IndexingJob, SyncService } from '../../core/services/sync.service';

@Component({
  selector: 'app-synchronization',
  imports: [CommonModule],
  templateUrl: './synchronization.html',
  styleUrl: './synchronization.scss',
})
export class Synchronization implements OnInit {
  readonly jobs = signal<IndexingJob[]>([]);
  readonly latestStatus = signal<IndexingJob | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  constructor(private readonly syncService: SyncService) {}

  ngOnInit(): void {
    this.loadJobs();
  }

  loadJobs(): void {
    this.loading.set(true);
    this.syncService.getJobs().subscribe({
      next: (jobs) => {
        this.jobs.set(jobs);
        this.latestStatus.set(jobs[0] ?? null);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load sync jobs.');
        this.loading.set(false);
      },
    });
  }

  triggerStart(): void {
    this.busy.set(true);
    this.error.set(null);
    this.syncService.start().subscribe({
      next: () => {
        this.busy.set(false);
        this.loadJobs();
      },
      error: () => {
        this.error.set('Failed to start sync.');
        this.busy.set(false);
      },
    });
  }

  triggerReindex(): void {
    this.busy.set(true);
    this.error.set(null);
    this.syncService.reindex().subscribe({
      next: () => {
        this.busy.set(false);
        this.loadJobs();
      },
      error: () => {
        this.error.set('Failed to trigger reindex.');
        this.busy.set(false);
      },
    });
  }
}
